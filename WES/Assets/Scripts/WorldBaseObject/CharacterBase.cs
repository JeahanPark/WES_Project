using Unity.Netcode;
using UnityEngine;

/// <summary>
/// "캐릭터" 공통 (플레이어/몬스터 둘 다 공유)
/// </summary>
public class CharacterBase : WorldEntityBase
{
    public const int DEFAULT_MAX_HP = 100;

    private const float MOVE_SPEED = 3.5f;
    private const float ROTATION_SPEED = 15f;

    // HP Network Variables
    private NetworkVariable<int> m_HP = new(DEFAULT_MAX_HP, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> m_MaxHP = new(DEFAULT_MAX_HP, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private event System.Action<int, int> m_OnHPChanged;

    private Vector2 m_MoveDirection;
    private Vector3 m_LookTarget;
    private bool m_HasLookTarget;

    // HP Properties
    public int HP => m_HP.Value;
    public int MaxHP => m_MaxHP.Value;
    public bool IsDead => m_HP.Value <= 0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        m_HP.OnValueChanged += OnHPValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        m_HP.OnValueChanged -= OnHPValueChanged;
    }

    public void SubscribeOnHPChanged(System.Action<int, int> _callback)
    {
        m_OnHPChanged += _callback;
    }

    public void UnsubscribeOnHPChanged(System.Action<int, int> _callback)
    {
        m_OnHPChanged -= _callback;
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

    protected virtual void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        UpdateMovement();
        UpdateRotation();
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

        if (isMoving)
        {
            Vector3 moveDirection = new(m_MoveDirection.x, 0f, m_MoveDirection.y);
            transform.position += moveDirection.normalized * (Time.deltaTime * MOVE_SPEED);
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
}
