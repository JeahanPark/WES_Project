using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 몬스터 기본 클래스
/// </summary>
public abstract class MonsterBase : CharacterBase
{
    // leash 거리 = 스폰영역 반경 × 이 배수 (director: 무한추격 금지)
    public const float LEASH_RADIUS_MULTIPLIER = 1.5f;
    // SpawnRadius 미지정(0) 시 leash 계산용 기본 반경
    private const float DEFAULT_LEASH_BASE_RADIUS = 5f;

    // R3-C 은신: 평시 반투명 알파. 노출 시 1.0. (가시성=NetworkVariable<bool>로 동기)
    private const float STEALTH_HIDDEN_ALPHA = 0.18f;
    // 은신 노출 근접 반경(이 거리 안에 플레이어가 들어오면 은신 해제).
    private const float STEALTH_REVEAL_RANGE = 2.5f;
    // R3-C 보스 페이즈 임계(HP 비율). 골격값 — 실수치 튜닝은 level-design.
    private const float BOSS_PHASE2_RATIO = 0.66f;
    private const float BOSS_PHASE3_RATIO = 0.33f;
    // R3-C 날씨강화: 강화 날씨일 때 이동속도 가속 배수(골격값 — 튜닝은 level-design).
    private const float WEATHER_BUFF_SPEED_MULTIPLIER = 1.4f;

    [SerializeField] private int m_MonsterId;
    [SerializeField] private StateAnimationComponent m_StateAnimationComponent;
    [SerializeField] private MonsterStateMachine m_StateMachine;
    [SerializeField] private Renderer m_Renderer;
    [SerializeField] private MonsterPerceptionComponent m_Perception;

    // R3-C 은신 가시성(서버 권위, 전원 동기). true=노출, false=은신(반투명). None/비은신은 항상 true.
    private NetworkVariable<bool> m_IsVisible = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // NOTE(R3-C 백로그): 은신 반투명은 m_Renderer 머티리얼 알파를 낮춘다. placeholder 머티리얼이
    // Standard Opaque면 알파가 시각적으로 안 보일 수 있다(가시성 로직·IsVisible NetworkVariable은 정상).
    // 메쉬/머티리얼 교체(designer 백로그) 시 Transparent 렌더 모드로 알파가 반영된다.
    private MonsterInfo m_MonsterInfo;
    private Color m_OriginalColor;
    private int m_SpawnAreaId;
    private float m_LeashBaseRadius = DEFAULT_LEASH_BASE_RADIUS;
    private int m_BossPhase = 1;
    private bool m_WeatherBuffActive;

    public int MonsterId => m_MonsterId;
    public int SpawnAreaId => m_SpawnAreaId;
    public StateAnimationComponent StateAnimationComponent => m_StateAnimationComponent;
    public MonsterPerceptionComponent Perception => m_Perception;

    // 행동 파라미터 (MonsterInfo 캐싱 — 서버에서 LoadMonsterInfo 시 채워짐). Info 미로드 시 비파괴 기본값.
    public float DetectRange => m_MonsterInfo != null ? m_MonsterInfo.DetectRange : 0f;
    public float AttackRange => m_MonsterInfo != null ? m_MonsterInfo.AttackRange : 1.5f;
    public float AttackCooldown => m_MonsterInfo != null ? m_MonsterInfo.AttackCooldown : 1.5f;
    public float ConfiguredMoveSpeed => m_MonsterInfo != null && m_MonsterInfo.MoveSpeed > 0f ? m_MonsterInfo.MoveSpeed : 2f;
    public MonsterBehaviorType BehaviorType => m_MonsterInfo != null ? m_MonsterInfo.BehaviorType : MonsterBehaviorType.None;
    public float LeashRadius => m_LeashBaseRadius * LEASH_RADIUS_MULTIPLIER;

    // R3-C 상태 노출용 getter(프로브/QA).
    public bool IsVisible => m_IsVisible.Value;
    public int BossPhase => m_BossPhase;

    protected virtual void Awake()
    {
        if (m_Renderer != null)
        {
            m_OriginalColor = m_Renderer.material.color;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        m_IsVisible.OnValueChanged += OnVisibilityChanged;

        if (IsServer)
        {
            LoadMonsterInfo();
        }

        if (m_StateMachine != null)
        {
            m_StateMachine.SetCollisionEnabled(true);
            m_StateMachine.ChangeState(MonsterStateType.Idle);
        }

        SetHitColor(false);
        ApplyVisibility(m_IsVisible.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        m_IsVisible.OnValueChanged -= OnVisibilityChanged;
    }

    public void SetSpawnAreaId(int _areaId)
    {
        m_SpawnAreaId = _areaId;
    }

    /// <summary>
    /// 스폰 시 의도 MonsterId를 주입하고 MonsterInfo를 재로딩한다(서버 권위).
    /// 모든 area 몬스터가 단일 placeholder 프리팹(직렬화 id=1)을 공유하므로,
    /// 스폰 직후 이 메서드로 정확한 id를 적용해야 HP/행동/드롭이 차별화된다.
    /// 풀 재사용 시에도 직전 id를 덮어쓰므로 안전.
    /// </summary>
    public void SetMonsterId(int _monsterId)
    {
        if (!IsServer)
        {
            GameDebug.LogWarning("[MonsterBase] SetMonsterId must be called on server only.");
            return;
        }

        m_MonsterId = _monsterId;
        LoadMonsterInfo();
    }

    /// <summary>
    /// leash 거리 계산용 스폰영역 반경 주입(스폰 시 AreaWorker가 호출).
    /// </summary>
    public void SetLeashBaseRadius(float _radius)
    {
        if (_radius > 0f)
            m_LeashBaseRadius = _radius;
    }

    public void SetHitColor(bool _isHit)
    {
        if (m_Renderer == null)
            return;

        m_Renderer.material.color = _isHit ? Color.red : m_OriginalColor;
    }

    protected override void OnDamaged(int _damage, CharacterBase _attacker, bool _isCritical)
    {
        base.OnDamaged(_damage, _attacker, _isCritical);

        if (IsDead || m_StateMachine == null)
            return;

        // 피격반격(director): DetectRange와 무관하게 공격자를 강제 타깃으로 주입.
        // 평화 몬스터(DetectRange=0)도 맞으면 1회 반격(Chase). 단 Perception 비활성이라
        // 추격 중 타깃 이탈 시 자연히 Idle 복귀(평화성 보존 — RB.7).
        if (IsServer && _attacker is PlayerCharacter player && m_Perception != null)
        {
            m_Perception.SetForcedTarget(player);
        }

        m_StateMachine.ChangeState(MonsterStateType.Hit);
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        if (IsServer)
        {
            UnsubscribeOnHPChanged(OnBossHPChanged);
            ExecuteMonsterDrop(transform.position);
            InGameController.Instance.AreaWorker.OnMonsterDied(this, m_SpawnAreaId);
        }

        if (m_StateMachine != null)
        {
            m_StateMachine.ChangeState(MonsterStateType.Death);
        }
    }

    private void ExecuteMonsterDrop(Vector3 _position)
    {
        if (m_MonsterInfo == null || m_MonsterInfo.DropTableId == 0)
            return;

        var dropItems = Managers.Info.DropTableItemInfoList.FindAll(x => x.DropTableId == m_MonsterInfo.DropTableId);
        foreach (var dropItem in dropItems)
        {
            if (dropItem.RewardType != RewardType.Item)
                continue;

            int count = UnityEngine.Random.Range(dropItem.Min, dropItem.Max + 1);
            if (count <= 0)
                continue;

            Vector3 spawnPos = _position + new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f));

            InGameController.Instance.PlayWorker.SpawnDropItem(dropItem.RewardId, count, spawnPos);
        }
    }

    private void LoadMonsterInfo()
    {
        if (m_MonsterId == 0)
            return;

        m_MonsterInfo = Managers.Info.MonsterInfoList.Find(x => x.Id == m_MonsterId);
        if (m_MonsterInfo == null)
        {
            GameDebug.LogWarning($"[MonsterBase] MonsterInfo not found: Id={m_MonsterId}");
            return;
        }

        SetMaxHP(m_MonsterInfo.MaxHP);
        SetHP(m_MonsterInfo.MaxHP);

        // 행동 파라미터를 상태머신/Perception에 반영(MoveSpeed, DetectRange 활성).
        if (m_StateMachine != null)
            m_StateMachine.ApplyMonsterInfo(this);

        if (m_Perception != null)
            m_Perception.Configure(this);

        // R3-C: 행동별 초기화(서버 권위).
        SetupBehavior();
    }

    // ============ R3-C 행동 분기 ============

    /// <summary>스폰/재로드 시 행동별 초기 상태(은신 가시성·보스 페이즈 구독)를 세팅한다(서버).</summary>
    private void SetupBehavior()
    {
        if (!IsServer)
            return;

        m_BossPhase = 1;

        // 은신: 평시 비가시(반투명). 추격/근접/안개 시 노출은 UpdateStealthVisibility(서버 Tick)에서 판정.
        m_IsVisible.Value = BehaviorType != MonsterBehaviorType.Stealth;

        // 보스: HP 변화로 페이즈 전환 — 중복 구독 방지 위해 먼저 해제 후 등록.
        UnsubscribeOnHPChanged(OnBossHPChanged);
        if (BehaviorType == MonsterBehaviorType.Boss)
            SubscribeOnHPChanged(OnBossHPChanged);
    }

    /// <summary>
    /// R3-C 행동 Tick(서버, 상태머신 Update 경로). 은신 가시성·날씨강화를 매 프레임 갱신한다.
    /// 비해당 BehaviorType은 즉시 반환(비파괴).
    /// </summary>
    public void UpdateBehaviorTick(bool _isChasing)
    {
        if (!IsServer)
            return;

        if (BehaviorType == MonsterBehaviorType.Stealth)
            UpdateStealthVisibility(_isChasing);
        else if (BehaviorType == MonsterBehaviorType.WeatherBuff)
            UpdateWeatherBuff();
    }

    /// <summary>은신 가시성 갱신. 추격중·근접·안개면 노출, 아니면 은신.</summary>
    private void UpdateStealthVisibility(bool _isChasing)
    {
        bool isFog = WeatherWorker.GlobalWeather == WeatherType.Fog;
        bool playerNearby = IsPlayerWithin(STEALTH_REVEAL_RANGE);
        bool exposed = _isChasing || playerNearby || isFog;

        if (m_IsVisible.Value != exposed)
            m_IsVisible.Value = exposed;
    }

    /// <summary>날씨강화: 눈보라/안개일 때 이동속도 가속(상태 변화 시에만 재적용).</summary>
    private void UpdateWeatherBuff()
    {
        WeatherType w = WeatherWorker.GlobalWeather;
        bool shouldBuff = w == WeatherType.Snowstorm || w == WeatherType.Fog;
        if (shouldBuff == m_WeatherBuffActive)
            return;

        m_WeatherBuffActive = shouldBuff;
        if (m_StateMachine != null)
            m_StateMachine.SetMoveSpeedMultiplier(shouldBuff ? WEATHER_BUFF_SPEED_MULTIPLIER : 1f);

        GameDebug.Log($"[MonsterBase] WeatherBuff {(shouldBuff ? "ON" : "OFF")} (Id={m_MonsterId}, weather={w}).");
    }

    private bool IsPlayerWithin(float _range)
    {
        var registry = InGameController.Instance?.ObjectDataWorker?.GetCharacterRegistry();
        if (registry == null)
            return false;

        Vector3 pos = transform.position;
        float sqrRange = _range * _range;
        foreach (var player in registry.GetAlivePlayers())
        {
            if (player == null)
                continue;
            if ((player.transform.position - pos).sqrMagnitude <= sqrRange)
                return true;
        }
        return false;
    }

    private void OnVisibilityChanged(bool _prev, bool _cur)
    {
        ApplyVisibility(_cur);
    }

    private void ApplyVisibility(bool _visible)
    {
        if (m_Renderer == null)
            return;

        Color c = m_Renderer.material.color;
        c.a = _visible ? 1f : STEALTH_HIDDEN_ALPHA;
        m_Renderer.material.color = c;
    }

    // 보스 HP 비율에 따라 페이즈 전환(66% / 33%). 페이즈마다 가속·공격 강화는 상태머신 속도 재적용으로 표현.
    private void OnBossHPChanged(int _current, int _max)
    {
        if (!IsServer || _max <= 0)
            return;

        float ratio = (float)_current / _max;
        int targetPhase = ratio <= BOSS_PHASE3_RATIO ? 3 : (ratio <= BOSS_PHASE2_RATIO ? 2 : 1);

        if (targetPhase <= m_BossPhase)
            return;

        m_BossPhase = targetPhase;
        OnBossPhaseEntered(targetPhase);
    }

    /// <summary>보스 페이즈 진입 시 강화(이동속도 가속). ATK 강화는 페이즈 배수로 GetATK 보정.</summary>
    private void OnBossPhaseEntered(int _phase)
    {
        // 페이즈별 이동속도 가속 = 기본 ×(1 + 0.25*(phase-1)).
        float speedMul = 1f + 0.25f * (_phase - 1);
        if (m_StateMachine != null)
            m_StateMachine.SetMoveSpeedMultiplier(speedMul);

        GameDebug.Log($"[MonsterBase] Boss phase {_phase} entered (Id={m_MonsterId}, speedMul={speedMul:F2}).");
    }

    /// <summary>보스 페이즈 ATK 배수(공격 강화). 비보스는 1.</summary>
    public float GetBossDamageMultiplier()
    {
        if (BehaviorType != MonsterBehaviorType.Boss)
            return 1f;
        return 1f + 0.3f * (m_BossPhase - 1);
    }
}
