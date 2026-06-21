using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 상태 머신
/// </summary>
public class MonsterStateMachine : MonoBehaviour
{
    private const float DEFAULT_MOVE_SPEED = 2f;
    private const float WANDER_RADIUS = 5f;

    [SerializeField] private MonsterBase m_Owner;

    private Dictionary<MonsterStateType, MonsterStateBase> m_States;
    private MonsterStateBase m_CurrentState;
    private MonsterStateType m_CurrentStateType = MonsterStateType.Idle;

    private Vector3 m_TargetPosition;
    private Vector3 m_SpawnPosition;
    private NavMeshAgent m_Agent;
    private float m_BaseMoveSpeed = DEFAULT_MOVE_SPEED;  // MonsterInfo 기본값
    private float m_SpeedMultiplier = 1f;                // R3-C: 보스 페이즈/날씨강화 가속 배수
    private float m_MoveSpeed = DEFAULT_MOVE_SPEED;      // 최종 적용 속도 = base × multiplier

    public MonsterBase Owner => m_Owner;
    public Vector3 SpawnPosition => m_SpawnPosition;
    public float MoveSpeed => m_MoveSpeed;
    public MonsterStateType CurrentStateType => m_CurrentStateType;

    private void Awake()
    {
        m_SpawnPosition = transform.position;
        m_Agent = GetComponent<NavMeshAgent>();
        InitializeStates();
    }

    private void Start()
    {
        ChangeState(MonsterStateType.Idle);
    }

    private void Update()
    {
        // 몬스터는 서버 권위. 상태머신(이동/상태전이)은 서버에서만 구동한다.
        // 비권위(클론)에서 돌면 NavMeshAgent 폴백 직접이동이 NetworkTransform과 충돌한다.
        if (m_Owner != null && !m_Owner.IsServer)
            return;

        // 감지 갱신(서버) — Chase/Attack의 입력원.
        if (m_Owner != null && m_Owner.Perception != null)
            m_Owner.Perception.Tick();

        // R3-C 행동 Tick(서버) — 은신 가시성·날씨강화. 추격 여부 = Chase/Attack 상태.
        if (m_Owner != null)
        {
            bool isChasing = m_CurrentStateType == MonsterStateType.Chase || m_CurrentStateType == MonsterStateType.Attack;
            m_Owner.UpdateBehaviorTick(isChasing);
        }

        m_CurrentState?.Update();
    }

    /// <summary>MonsterInfo 행동 파라미터 반영(서버, LoadMonsterInfo 후). MoveSpeed → NavMeshAgent.</summary>
    public void ApplyMonsterInfo(MonsterBase _owner)
    {
        if (_owner == null)
            return;

        m_BaseMoveSpeed = _owner.ConfiguredMoveSpeed;
        m_SpeedMultiplier = 1f;
        ApplyFinalSpeed();
    }

    /// <summary>R3-C: 보스 페이즈/날씨강화 가속 배수 적용(서버). base × multiplier로 최종 속도 재계산.</summary>
    public void SetMoveSpeedMultiplier(float _multiplier)
    {
        m_SpeedMultiplier = _multiplier > 0f ? _multiplier : 1f;
        ApplyFinalSpeed();
    }

    private void ApplyFinalSpeed()
    {
        m_MoveSpeed = m_BaseMoveSpeed * m_SpeedMultiplier;
        if (m_Agent != null)
            m_Agent.speed = m_MoveSpeed;
    }

    public void ChangeState(MonsterStateType _stateType)
    {
        if (!m_States.TryGetValue(_stateType, out var newState))
            return;

        m_CurrentState?.Exit();
        m_CurrentState = newState;
        m_CurrentStateType = _stateType;
        m_CurrentState?.Enter();
    }

    public void SetRandomTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * WANDER_RADIUS;
        Vector3 candidate = m_SpawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

        // NavMesh 위의 유효한 위치로 보정
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, WANDER_RADIUS, NavMesh.AllAreas))
        {
            m_TargetPosition = hit.position;
        }
        else
        {
            m_TargetPosition = candidate;
        }
    }

    /// <summary>추격용: 목표 위치를 직접 설정(타깃 플레이어 위치 또는 스폰 복귀 지점).</summary>
    public void SetMoveTarget(Vector3 _position)
    {
        m_TargetPosition = _position;
    }

    public bool HasReachedTarget(float _threshold)
    {
        if (m_Agent != null && m_Agent.isOnNavMesh)
        {
            return !m_Agent.pathPending && m_Agent.remainingDistance <= _threshold;
        }

        float distance = Vector3.Distance(transform.position, m_TargetPosition);
        return distance <= _threshold;
    }

    public void MoveTowardsTarget()
    {
        // NavMeshAgent 사용
        if (m_Agent != null && m_Agent.isOnNavMesh)
        {
            m_Agent.SetDestination(m_TargetPosition);
            return;
        }

        // NavMesh 없을 때 폴백: 직접 이동
        Vector3 direction = (m_TargetPosition - transform.position).normalized;
        direction.y = 0f;

        transform.position += direction * (m_MoveSpeed * Time.deltaTime);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    /// <summary>고정 방향으로 1프레임 직선 이동(Charge 돌진). NavMesh 경계는 SamplePosition으로만 보정.</summary>
    public void MoveStraight(Vector3 _direction, float _speed)
    {
        _direction.y = 0f;
        if (_direction.sqrMagnitude < 0.0001f)
            return;

        _direction.Normalize();
        Vector3 next = transform.position + _direction * (_speed * Time.deltaTime);

        if (m_Agent != null && m_Agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(next, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                m_Agent.Warp(hit.position);
        }
        else
        {
            transform.position = next;
        }

        Quaternion rot = Quaternion.LookRotation(_direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
    }

    /// <summary>현재 위치에서 스폰 지점까지의 거리(leash 판정).</summary>
    public float DistanceFromSpawn()
    {
        return Vector3.Distance(transform.position, m_SpawnPosition);
    }

    public float DistanceTo(Vector3 _position)
    {
        return Vector3.Distance(transform.position, _position);
    }

    public void SetCollisionEnabled(bool _enabled)
    {
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = _enabled;
        }
    }

    public void RequestDespawn()
    {
        if (m_Owner.IsServer && m_Owner.IsSpawned)
        {
            m_Owner.NetworkObject.Despawn();
        }
    }

    private void InitializeStates()
    {
        m_States = new Dictionary<MonsterStateType, MonsterStateBase>
        {
            { MonsterStateType.Idle, new MonsterIdleState() },
            { MonsterStateType.Walk, new MonsterWalkState() },
            { MonsterStateType.Chase, new MonsterChaseState() },
            { MonsterStateType.Attack, new MonsterAttackState() },
            { MonsterStateType.Hit, new MonsterHitState() },
            { MonsterStateType.Death, new MonsterDeathState() }
        };

        foreach (var state in m_States.Values)
        {
            state.Initialize(this);
        }
    }
}
