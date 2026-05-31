using UnityEngine;
using UnityEngine.AI;

public class NightMonsterComponent : MonoBehaviour
{
    private const float AVOID_CHECK_INTERVAL = 0.5f;
    private const float AVOID_RETREAT_DISTANCE = 8f;

    [SerializeField] private DayNightConfig m_Config;

    private NavMeshAgent m_Agent;
    private float m_AvoidCheckTimer;
    private bool m_IsActive;

    private void Awake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        DayNightWorker.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        DayNightWorker.OnPhaseChanged -= OnPhaseChanged;
    }

    private void Start()
    {
        var dayNightWorker = InGameController.Instance?.DayNightWorker;
        if (dayNightWorker != null)
        {
            DayPhase phase = dayNightWorker.CurrentPhase;
            m_IsActive = phase == DayPhase.Night || phase == DayPhase.Dawn;
        }
    }

    private void Update()
    {
        if (!m_IsActive)
            return;

        m_AvoidCheckTimer += Time.deltaTime;
        if (m_AvoidCheckTimer < AVOID_CHECK_INTERVAL)
            return;

        m_AvoidCheckTimer = 0f;
        CheckCampfireAvoidance();
    }

    private void OnPhaseChanged(DayPhase _prev, DayPhase _current)
    {
        m_IsActive = _current == DayPhase.Night || _current == DayPhase.Dawn;
    }

    private void CheckCampfireAvoidance()
    {
        if (m_Config == null || m_Agent == null)
            return;

        float avoidRadius = m_Config.NightMonsterCampfireAvoidRadius;
        var buildings = WorldBuildingObject.ActiveBuildings;

        foreach (var building in buildings)
        {
            if (building == null || building.BuildingInfoId != 1)
                continue;

            float dist = Vector3.Distance(transform.position, building.transform.position);
            if (dist >= avoidRadius)
                continue;

            RetreatFromPosition(building.transform.position);
            return;
        }
    }

    private void RetreatFromPosition(Vector3 _dangerPosition)
    {
        if (!m_Agent.isOnNavMesh)
            return;

        Vector3 awayDirection = (transform.position - _dangerPosition).normalized;
        Vector3 retreatTarget = transform.position + awayDirection * AVOID_RETREAT_DISTANCE;

        if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, AVOID_RETREAT_DISTANCE, NavMesh.AllAreas))
        {
            m_Agent.SetDestination(hit.position);
        }
    }
}
