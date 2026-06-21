using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// "캐릭터" 공통 (플레이어/몬스터 둘 다 공유)
/// </summary>
public class CharacterBase : WorldEntityBase
{
    public const int DEFAULT_MAX_HP = 100;
    public const int DEFAULT_ATK = 10;
    public const int DEFAULT_DEF = 3;
    public const float DEFAULT_HP_REGEN = 0.5f;
    public const float DEFAULT_MOVE_SPEED = 5.0f;
    public const float CRITICAL_CHANCE = 0.1f;
    public const float CRITICAL_MULTIPLIER = 1.5f;

    private const float ROTATION_SPEED = 15f;
    private const float PATH_CORNER_ARRIVE_DISTANCE = 0.4f;

    // 이동 시 NavMesh 경계 판정.
    // 수직(반경)은 언덕/슬로프 표면 높이차를 흡수할 만큼 넉넉히 두고,
    // 수평 허용치는 바다(섬 외곽)로 새는 것을 막기 위해 좁게 유지한다.
    private const float NAV_MOVE_SAMPLE_RADIUS = 3.0f;
    private const float NAV_MOVE_HORIZONTAL_TOLERANCE = 0.6f;
    // MoveTo 목적지를 NavMesh 표면에 투영할 때의 탐색 반경.
    private const float MOVE_DEST_SAMPLE_RADIUS = 5.0f;

    [SerializeField] private CharacterScriptable m_CharacterData;
    [SerializeField] private int m_ATK = DEFAULT_ATK;
    [SerializeField] private int m_DEF = DEFAULT_DEF;
    [SerializeField] private float m_HPRegen = DEFAULT_HP_REGEN;
    [SerializeField] private float m_MoveSpeed = DEFAULT_MOVE_SPEED;

    // HP Network Variables
    private NetworkVariable<int> m_HP = new(DEFAULT_MAX_HP, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> m_MaxHP = new(DEFAULT_MAX_HP, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private event System.Action<int, int> m_OnHPChanged;
    private event System.Action<int, CharacterBase, bool> m_OnDamaged;
    private event System.Action m_OnDeath;

    private CharacterWorldUI m_WorldUI;
    private NavMeshAgent m_NavAgent;
    private Vector2 m_MoveDirection;
    private Vector3 m_LookTarget;
    private bool m_HasLookTarget;
    private float m_HPRegenAccumulator = 0f;

    // NavMesh 경로 추종 (MoveTo)
    private Vector3[] m_PathCorners;
    private int m_PathCornerIndex;
    private bool m_IsFollowingPath;

    // HP Properties
    public int HP => m_HP.Value;
    public int MaxHP => m_MaxHP.Value;
    public bool IsDead => m_HP.Value <= 0;
    public bool IsFollowingPath => m_IsFollowingPath;
    public Vector3 WorldUIOffset => m_CharacterData != null ? m_CharacterData.WorldUIOffset : Vector3.up * 2f;

    public int GetATK() => m_ATK;
    public int GetDEF() => m_DEF;
    public float GetHPRegen() => m_HPRegen;
    public float GetMoveSpeed() => m_MoveSpeed;

    public void SetATK(int _value) { m_ATK = _value; }
    public void SetDEF(int _value) { m_DEF = _value; }
    public void SetHPRegen(float _value) { m_HPRegen = _value; }
    public void SetMoveSpeed(float _value) { m_MoveSpeed = _value; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        m_HP.OnValueChanged += OnHPValueChanged;
        CreateWorldUI();
        RegisterToCharacterRegistry();
        InitializeNavAgent();
    }

    private void InitializeNavAgent()
    {
        m_NavAgent = GetComponent<NavMeshAgent>();
        if (m_NavAgent != null)
        {
            m_NavAgent.speed = m_MoveSpeed;
            m_NavAgent.angularSpeed = ROTATION_SPEED * 30f;
            m_NavAgent.acceleration = 20f;
            m_NavAgent.stoppingDistance = 0.1f;
            m_NavAgent.updateRotation = false; // 회전은 직접 처리

            // 이동 권위 인스턴스에서만 NavMeshAgent를 활성화한다.
            // 비권위(클론) 인스턴스에서 Agent가 켜져 있으면 매 프레임 transform.position을
            // 자기 내부 위치로 덮어써 NetworkTransform이 적용한 동기화 위치를 되돌린다(frozen 버그).
            // 비권위 인스턴스는 Agent를 끄고 NetworkTransform이 transform을 구동하게 둔다.
            m_NavAgent.enabled = ShouldEnableNavAgent();
        }
    }

    /// <summary>
    /// NavMeshAgent를 활성화할(=이동 권위를 가진) 인스턴스인지 여부.
    /// 기본은 서버 권위(NetworkTransform 서버권위 객체, 예: 몬스터).
    /// 오너 권위 이동 객체(예: 플레이어 + ClientNetworkTransform)는 IsOwner로 오버라이드한다.
    /// </summary>
    protected virtual bool ShouldEnableNavAgent()
    {
        return IsServer;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        m_HP.OnValueChanged -= OnHPValueChanged;
        ReleaseWorldUI();
        UnregisterFromCharacterRegistry();
    }

    private void RegisterToCharacterRegistry()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        InGameController.Instance.ObjectDataWorker.GetCharacterRegistry().RegisterCharacter(this);
    }

    private void UnregisterFromCharacterRegistry()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        InGameController.Instance.ObjectDataWorker.GetCharacterRegistry().UnregisterCharacter(NetworkObjectId);
    }

    public void SubscribeOnHPChanged(System.Action<int, int> _callback)
    {
        m_OnHPChanged += _callback;
    }

    public void UnsubscribeOnHPChanged(System.Action<int, int> _callback)
    {
        m_OnHPChanged -= _callback;
    }

    public void SubscribeOnDamaged(System.Action<int, CharacterBase, bool> _callback)
    {
        m_OnDamaged += _callback;
    }

    public void UnsubscribeOnDamaged(System.Action<int, CharacterBase, bool> _callback)
    {
        m_OnDamaged -= _callback;
    }

    public void SubscribeOnDeath(System.Action _callback)
    {
        m_OnDeath += _callback;
    }

    public void UnsubscribeOnDeath(System.Action _callback)
    {
        m_OnDeath -= _callback;
    }

    public void SetHP(int _value)
    {
        if (!IsServer) return;
        m_HP.Value = System.Math.Clamp(_value, 0, m_MaxHP.Value);
    }

    public void SetMaxHP(int _value)
    {
        if (!IsServer) return;
        m_MaxHP.Value = _value;
    }

    /// <summary>
    /// HP 증감 (양수: 힐, 음수: 데미지)
    /// </summary>
    public void AddHP(int _amount)
    {
        AddHPServerRpc(_amount);
    }

    [Rpc(SendTo.Server)]
    private void AddHPServerRpc(int _amount)
    {
        SetHP(m_HP.Value + _amount);
    }

    /// <summary>
    /// 데미지 받기 (공격자로부터)
    /// </summary>
    public void TakeDamage(int _damage, CharacterBase _attacker)
    {
        if (IsDead)
            return;

        ulong attackerId = _attacker != null ? _attacker.NetworkObjectId : 0;
        TakeDamageServerRpc(_damage, attackerId);
    }

    [Rpc(SendTo.Server)]
    private void TakeDamageServerRpc(int _damage, ulong _attackerId)
    {
        if (IsDead)
            return;

        bool isCritical = UnityEngine.Random.value < CRITICAL_CHANCE;
        float multiplier = isCritical ? CRITICAL_MULTIPLIER : 1f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt((_damage - m_DEF) * multiplier));
        SetHP(m_HP.Value - finalDamage);

        OnDamagedClientRpc(finalDamage, _attackerId, isCritical);

        if (IsDead)
        {
            OnDeathClientRpc();
        }
    }

    /// <summary>
    /// 환경 데미지 적용 (서버 전용). DEF/크리티컬 무시, 가공 없는 고정 데미지.
    /// 공격자 없음(_attackerId = 0)으로 기존 데미지 표시/사망 경로를 재사용한다.
    /// _allowDeath == false면 HP 1 미만으로 내려가지 않도록 보호한다.
    /// </summary>
    protected void ApplyEnvironmentDamageServer(int _damage, bool _allowDeath)
    {
        if (!IsServer)
            return;

        if (IsDead)
            return;

        int finalDamage = Mathf.Max(0, _damage);
        int targetHP = m_HP.Value - finalDamage;
        if (!_allowDeath)
            targetHP = Mathf.Max(1, targetHP);

        SetHP(targetHP);

        OnDamagedClientRpc(finalDamage, 0, false);

        if (IsDead)
        {
            OnDeathClientRpc();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void OnDamagedClientRpc(int _damage, ulong _attackerId, bool _isCritical)
    {
        CharacterBase attacker = null;

        if (_attackerId != 0 && NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_attackerId, out var attackerObj))
            {
                attacker = attackerObj.GetComponent<CharacterBase>();
            }
        }

        m_OnDamaged?.Invoke(_damage, attacker, _isCritical);
        OnDamaged(_damage, attacker, _isCritical);
    }

    [Rpc(SendTo.Everyone)]
    private void OnDeathClientRpc()
    {
        m_OnDeath?.Invoke();
        OnDeath();
    }

    /// <summary>
    /// 피격 시 호출 (자식 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnDamaged(int _damage, CharacterBase _attacker, bool _isCritical)
    {
        Managers.Audio?.PlaySfx(AudioKey.SFX_HIT); // R4 ③ 피격 SFX(모든 클라 로컬, 음원0=무음)

        if (InGameController.Instance == null || InGameController.Instance.WorldUIWorker == null)
            return;

        Vector3 spawnPosition = transform.position + WorldUIOffset + Vector3.up * 0.3f;
        InGameController.Instance.WorldUIWorker.CreateDamageNumber(_damage, spawnPosition, _isCritical);
    }

    /// <summary>
    /// 사망 시 호출 (자식 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnDeath()
    {
    }

    protected virtual void Update()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
        {
            UpdateHPRegen(Time.deltaTime);
        }

        if (IsOwner)
        {
            UpdatePathFollow();
            UpdateMovement();
            UpdateRotation();
        }
    }

    private void UpdateHPRegen(float _deltaTime)
    {
        if (IsDead || m_HP.Value >= m_MaxHP.Value || m_HPRegen <= 0f)
            return;

        m_HPRegenAccumulator += m_HPRegen * _deltaTime;
        if (m_HPRegenAccumulator >= 1f)
        {
            int regenAmount = (int)m_HPRegenAccumulator;
            m_HPRegenAccumulator -= regenAmount;
            SetHP(m_HP.Value + regenAmount);
        }
    }

    public void MoveWithDirection(Vector2 _direction)
    {
        if (IsDead)
        {
            m_MoveDirection = Vector2.zero;
            return;
        }

        // 실제 수동 입력이 있을 때만 경로 추종을 취소하고 수동 이동으로 전환한다.
        // (PlayerCharacter는 입력이 없어도 매 프레임 MoveWithDirection(zero)를 호출하므로,
        //  0 입력에도 취소하면 MoveTo 경로 추종이 첫 프레임에 끊긴다.)
        if (_direction.sqrMagnitude > 0.0001f)
        {
            if (m_IsFollowingPath)
                StopMove();
            m_MoveDirection = _direction;
        }
        else if (!m_IsFollowingPath)
        {
            // 경로 추종 중이 아닐 때만 정지를 반영한다(추종 중이면 추종 방향 유지).
            m_MoveDirection = Vector2.zero;
        }
    }

    /// <summary>
    /// NavMesh 경로탐색으로 목표 지점까지 이동을 시작한다.
    /// 매 프레임 다음 코너 방향으로 기존 이동 로직(MoveWithDirection 경로)을 사용해 추종한다.
    /// </summary>
    /// <returns>경로 계산 성공 여부. false면 호출측에서 Warp 등 폴백 처리.</returns>
    public bool MoveTo(Vector3 _destination)
    {
        if (IsDead)
            return false;

        if (m_NavAgent == null || !m_NavAgent.isOnNavMesh)
            return false;

        // 목적지가 NavMesh 표면에서 벗어난 raw 좌표(예: 트리거 콜라이더 중심 y)면
        // CalculatePath가 부분경로(PathPartial)를 반환해 추종이 즉시 끊긴다.
        // 먼저 NavMesh에 투영해 도달 가능한 표면 좌표로 보정한다.
        if (NavMesh.SamplePosition(_destination, out NavMeshHit destHit, MOVE_DEST_SAMPLE_RADIUS, NavMesh.AllAreas))
            _destination = destHit.position;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, _destination, NavMesh.AllAreas, path))
            return false;

        // 부분경로(PathPartial)는 목적지에 도달하지 못하므로 폴백 처리하게 한다.
        if (path.status != NavMeshPathStatus.PathComplete || path.corners == null || path.corners.Length < 2)
            return false;

        m_PathCorners = path.corners;
        m_PathCornerIndex = 1; // corners[0]은 시작점이므로 1번부터 추종
        m_IsFollowingPath = true;
        return true;
    }

    /// <summary>
    /// 진행 중인 이동(경로 추종 및 수동 방향)을 모두 정지한다.
    /// </summary>
    public void StopMove()
    {
        m_IsFollowingPath = false;
        m_PathCorners = null;
        m_PathCornerIndex = 0;
        m_MoveDirection = Vector2.zero;
    }

    public void LookAtPosition(Vector3 _worldPosition)
    {
        m_LookTarget = _worldPosition;
        m_HasLookTarget = true;
    }

    public virtual void Attack()
    {
    }

    public virtual void Interact()
    {
    }

    protected virtual void OnWalkChanged(bool _isWalking)
    {
    }

    private void UpdatePathFollow()
    {
        if (!m_IsFollowingPath)
            return;

        if (IsDead || m_PathCorners == null || m_PathCornerIndex >= m_PathCorners.Length)
        {
            StopMove();
            return;
        }

        Vector3 target = m_PathCorners[m_PathCornerIndex];
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        // 현재 코너 도달 → 다음 코너로
        if (toTarget.sqrMagnitude <= PATH_CORNER_ARRIVE_DISTANCE * PATH_CORNER_ARRIVE_DISTANCE)
        {
            m_PathCornerIndex++;
            if (m_PathCornerIndex >= m_PathCorners.Length)
            {
                StopMove();
                return;
            }
            target = m_PathCorners[m_PathCornerIndex];
            toTarget = target - transform.position;
            toTarget.y = 0f;
        }

        // 경로 추종 중에는 진행 방향을 바라보게 하고 이동 방향을 직접 설정한다.
        // (MoveWithDirection은 경로를 취소하므로 m_MoveDirection을 직접 갱신)
        Vector3 dir = toTarget.normalized;
        m_MoveDirection = new Vector2(dir.x, dir.z);
        LookAtPosition(target);
    }

    private void UpdateMovement()
    {
        bool isMoving = m_MoveDirection.sqrMagnitude > 0f;

        OnWalkChanged(isMoving);

        if (m_NavAgent != null && m_NavAgent.isOnNavMesh)
        {
            if (isMoving)
            {
                Vector3 moveDirection = new Vector3(m_MoveDirection.x, 0f, m_MoveDirection.y).normalized;
                Vector3 delta = moveDirection * (Time.deltaTime * m_MoveSpeed);
                Vector3 nextPos = transform.position + delta;
                // NavMesh 경계 차단: 다음 위치 근처에 NavMesh가 있어야 이동 허용.
                // 언덕/슬로프로 표면이 솟은 구간(수직 차이)은 통과시키되,
                // 바다(외곽)로 새는 것은 막기 위해 샘플점과의 '수평' 거리만 엄격히 본다.
                if (NavMesh.SamplePosition(nextPos, out NavMeshHit nh, NAV_MOVE_SAMPLE_RADIUS, NavMesh.AllAreas))
                {
                    float dx = nh.position.x - nextPos.x;
                    float dz = nh.position.z - nextPos.z;
                    if (dx * dx + dz * dz <= NAV_MOVE_HORIZONTAL_TOLERANCE * NAV_MOVE_HORIZONTAL_TOLERANCE)
                        m_NavAgent.Move(delta);
                }
            }
        }
        else
        {
            // NavMesh 없을 때 폴백: 직접 이동
            if (isMoving)
            {
                Vector3 moveDirection = new(m_MoveDirection.x, 0f, m_MoveDirection.y);
                transform.position += moveDirection.normalized * (Time.deltaTime * m_MoveSpeed);
            }
        }
    }

    private void UpdateRotation()
    {
        if (!m_HasLookTarget)
            return;

        Vector3 lookDirection = m_LookTarget - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * ROTATION_SPEED);
        }
    }

    private void OnHPValueChanged(int _prev, int _current)
    {
        m_OnHPChanged?.Invoke(_current, m_MaxHP.Value);
    }

    private void CreateWorldUI()
    {
        if (InGameController.Instance == null)
        {
            GameDebug.LogWarning($"[CharacterBase] CreateWorldUI failed: InGameController.Instance is null");
            return;
        }

        if (InGameController.Instance.WorldUIWorker == null)
        {
            GameDebug.LogWarning($"[CharacterBase] CreateWorldUI failed: WorldUIWorker is null");
            return;
        }

        m_WorldUI = InGameController.Instance.WorldUIWorker.CreateCharacterWorldUI(this, transform);
        GameDebug.Log($"[CharacterBase] WorldUI created: {m_WorldUI != null}");
    }

    private void ReleaseWorldUI()
    {
        if (m_WorldUI == null)
            return;

        if (InGameController.Instance != null && InGameController.Instance.WorldUIWorker != null)
        {
            InGameController.Instance.WorldUIWorker.ReleaseWorldUI(m_WorldUI);
        }

        m_WorldUI = null;
    }
}
