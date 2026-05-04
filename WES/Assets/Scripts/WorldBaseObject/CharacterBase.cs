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

    // HP Properties
    public int HP => m_HP.Value;
    public int MaxHP => m_MaxHP.Value;
    public bool IsDead => m_HP.Value <= 0;
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
        }
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
        m_MoveDirection = _direction;
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
                // NavMesh 경계 차단: 다음 위치가 NavMesh 위가 아니면 이동 무시
                if (NavMesh.SamplePosition(nextPos, out NavMeshHit nh, 0.5f, NavMesh.AllAreas))
                {
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
        GameDebug.Log($"[CharacterBase] OnHPValueChanged: {_prev} -> {_current}, Subscribers: {m_OnHPChanged?.GetInvocationList()?.Length ?? 0}");
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
