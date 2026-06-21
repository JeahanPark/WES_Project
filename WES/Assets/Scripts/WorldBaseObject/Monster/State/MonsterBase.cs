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

    [SerializeField] private int m_MonsterId;
    [SerializeField] private StateAnimationComponent m_StateAnimationComponent;
    [SerializeField] private MonsterStateMachine m_StateMachine;
    [SerializeField] private Renderer m_Renderer;
    [SerializeField] private MonsterPerceptionComponent m_Perception;

    private MonsterInfo m_MonsterInfo;
    private Color m_OriginalColor;
    private int m_SpawnAreaId;
    private float m_LeashBaseRadius = DEFAULT_LEASH_BASE_RADIUS;

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
    }
}
