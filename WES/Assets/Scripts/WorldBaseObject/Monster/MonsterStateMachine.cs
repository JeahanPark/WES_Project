using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터 상태 머신
/// </summary>
public class MonsterStateMachine : MonoBehaviour
{
    private const float MOVE_SPEED = 2f;
    private const float WANDER_RADIUS = 5f;

    [SerializeField] private MonsterBase m_Owner;

    private Dictionary<MonsterStateType, MonsterStateBase> m_States;
    private MonsterStateBase m_CurrentState;

    private Vector3 m_TargetPosition;
    private Vector3 m_SpawnPosition;
    private NavMeshAgent m_Agent;

    public MonsterBase Owner => m_Owner;

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

        m_CurrentState?.Update();
    }

    public void ChangeState(MonsterStateType _stateType)
    {
        if (!m_States.TryGetValue(_stateType, out var newState))
            return;

        m_CurrentState?.Exit();
        m_CurrentState = newState;
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

        transform.position += direction * (MOVE_SPEED * Time.deltaTime);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
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
            { MonsterStateType.Hit, new MonsterHitState() },
            { MonsterStateType.Death, new MonsterDeathState() }
        };

        foreach (var state in m_States.Values)
        {
            state.Initialize(this);
        }
    }
}
