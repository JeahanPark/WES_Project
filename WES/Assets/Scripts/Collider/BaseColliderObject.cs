using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개별 충돌 판정 객체 (순수 C# 클래스)
/// - OverlapSphere 기반 판정
/// - 다중 히트 방지
/// - 생명주기 지원 (즉시 판정 / 지속 판정)
/// - Transform 따라가기 지원
/// </summary>
public class BaseColliderObject
{
    private const int HIT_BUFFER_SIZE = 10;

    private CharacterBase m_Owner;
    private Transform m_FollowTarget;
    private Vector3 m_Offset;
    private Vector3 m_Position;
    private float m_Radius;
    private int m_Damage;
    private int m_MaxHitCount;
    private LayerMask m_TargetLayer;
    private float m_Duration;
    private float m_RemainingTime;

    private HashSet<ulong> m_HitTargets;
    private Collider[] m_HitBuffer;
    private int m_CurrentHitCount;

    public Vector3 Position => m_Position;
    public float Radius => m_Radius;
    public bool HasHit => m_CurrentHitCount > 0;
    public bool IsExpired => m_Duration > 0f && m_RemainingTime <= 0f;
    public bool IsInstant => m_Duration <= 0f;

    public BaseColliderObject()
    {
        m_HitTargets = new HashSet<ulong>();
        m_HitBuffer = new Collider[HIT_BUFFER_SIZE];
    }

    /// <summary>
    /// 고정 위치 충돌 객체 초기화
    /// </summary>
    public void Initialize(CharacterBase _owner, Vector3 _position, float _radius, int _damage, int _maxHitCount, LayerMask _targetLayer, float _duration)
    {
        m_Owner = _owner;
        m_FollowTarget = null;
        m_Offset = Vector3.zero;
        m_Position = _position;
        m_Radius = _radius;
        m_Damage = _damage;
        m_MaxHitCount = _maxHitCount;
        m_TargetLayer = _targetLayer;
        m_Duration = _duration;
        m_RemainingTime = _duration;

        m_HitTargets.Clear();
        m_CurrentHitCount = 0;
    }

    /// <summary>
    /// Transform 따라가기 충돌 객체 초기화
    /// </summary>
    public void Initialize(CharacterBase _owner, Transform _followTarget, Vector3 _offset, float _radius, int _damage, int _maxHitCount, LayerMask _targetLayer, float _duration)
    {
        m_Owner = _owner;
        m_FollowTarget = _followTarget;
        m_Offset = _offset;
        m_Position = _followTarget != null ? _followTarget.position + _offset : _offset;
        m_Radius = _radius;
        m_Damage = _damage;
        m_MaxHitCount = _maxHitCount;
        m_TargetLayer = _targetLayer;
        m_Duration = _duration;
        m_RemainingTime = _duration;

        m_HitTargets.Clear();
        m_CurrentHitCount = 0;
    }

    /// <summary>
    /// 충돌 판정 수행
    /// </summary>
    public void PerformDetection()
    {
        if (m_MaxHitCount > 0 && m_CurrentHitCount >= m_MaxHitCount)
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            m_Position,
            m_Radius,
            m_HitBuffer,
            m_TargetLayer
        );

        for (int i = 0; i < hitCount; i++)
        {
            if (m_MaxHitCount > 0 && m_CurrentHitCount >= m_MaxHitCount)
                break;

            ProcessHit(m_HitBuffer[i]);
        }
    }

    /// <summary>
    /// 매 프레임 업데이트 (지속 판정용)
    /// </summary>
    public void Update(float _deltaTime)
    {
        if (IsInstant)
            return;

        // Transform 따라가기
        if (m_FollowTarget != null)
        {
            m_Position = m_FollowTarget.position + m_Offset;
        }

        // 시간 감소
        m_RemainingTime -= _deltaTime;

        // 충돌 판정
        if (!IsExpired)
        {
            PerformDetection();
        }
    }

    /// <summary>
    /// 초기화 (풀 반환 전)
    /// </summary>
    public void Reset()
    {
        m_Owner = null;
        m_FollowTarget = null;
        m_HitTargets.Clear();
        m_CurrentHitCount = 0;
        m_RemainingTime = 0f;
    }

    private void ProcessHit(Collider _collider)
    {
        CharacterBase target = _collider.GetComponentInParent<CharacterBase>();

        if (target == null)
            return;

        if (target == m_Owner)
            return;

        if (target.IsDead)
            return;

        if (m_HitTargets.Contains(target.NetworkObjectId))
            return;

        m_HitTargets.Add(target.NetworkObjectId);
        m_CurrentHitCount++;

        target.TakeDamage(m_Damage, m_Owner);
    }
}
