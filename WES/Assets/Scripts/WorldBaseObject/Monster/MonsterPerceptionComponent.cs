using UnityEngine;

/// <summary>
/// 몬스터 감지 컴포넌트 (서버 전용).
/// CharacterRegistry의 살아있는 플레이어 중 DetectRange 내 최근접을 타깃으로 산출한다.
/// DetectRange=0(평화)이면 자발 탐색을 하지 않는다. 피격반격은 SetForcedTarget로 외부 주입.
/// </summary>
public class MonsterPerceptionComponent : MonoBehaviour
{
    // 피격반격 강제 타깃 유지 시간(초). 이 시간 동안은 DetectRange 밖이어도 타깃 유지(반격).
    private const float FORCED_TARGET_DURATION = 6f;

    [SerializeField] private MonsterBase m_Owner;

    private float m_DetectRange;
    private PlayerCharacter m_CurrentTarget;
    private PlayerCharacter m_ForcedTarget;
    private float m_ForcedTimer;

    public PlayerCharacter CurrentTarget => m_CurrentTarget;
    public bool HasTarget => m_CurrentTarget != null && !m_CurrentTarget.IsDead;

    /// <summary>MonsterInfo 로드 후 감지 반경 적용(서버에서 호출).</summary>
    public void Configure(MonsterBase _owner)
    {
        m_Owner = _owner;
        m_DetectRange = _owner != null ? _owner.DetectRange : 0f;
    }

    /// <summary>피격반격: 공격자를 강제 타깃으로 주입(DetectRange 무관, 일정 시간 유지).</summary>
    public void SetForcedTarget(PlayerCharacter _target)
    {
        if (_target == null || _target.IsDead)
            return;

        m_ForcedTarget = _target;
        m_ForcedTimer = FORCED_TARGET_DURATION;
        m_CurrentTarget = _target;
    }

    /// <summary>서버 권위에서만 타깃을 갱신한다. 상태머신 Update 경로에서 호출.</summary>
    public void Tick()
    {
        if (m_Owner == null || !m_Owner.IsServer)
            return;

        UpdateForcedTarget();

        // 강제 타깃 유효 시 우선 유지(반격 추격).
        if (m_ForcedTarget != null)
        {
            m_CurrentTarget = m_ForcedTarget;
            return;
        }

        // 평화(DetectRange=0)는 자발 탐색 안 함.
        if (m_DetectRange <= 0f)
        {
            m_CurrentTarget = null;
            return;
        }

        m_CurrentTarget = FindNearestPlayerInRange();
    }

    /// <summary>타깃이 DetectRange를 벗어났는지(leash 판정 보조). 강제 타깃은 시간 만료까지 유지.</summary>
    public bool IsTargetOutOfDetectRange()
    {
        if (m_CurrentTarget == null || m_CurrentTarget.IsDead)
            return true;

        if (m_ForcedTarget != null)
            return false;

        if (m_DetectRange <= 0f)
            return true;

        float sqr = (m_CurrentTarget.transform.position - transform.position).sqrMagnitude;
        return sqr > m_DetectRange * m_DetectRange;
    }

    public void ClearTarget()
    {
        m_CurrentTarget = null;
        m_ForcedTarget = null;
        m_ForcedTimer = 0f;
    }

    private void UpdateForcedTarget()
    {
        if (m_ForcedTarget == null)
            return;

        m_ForcedTimer -= Time.deltaTime;
        if (m_ForcedTimer <= 0f || m_ForcedTarget.IsDead)
        {
            m_ForcedTarget = null;
            m_ForcedTimer = 0f;
        }
    }

    private PlayerCharacter FindNearestPlayerInRange()
    {
        var registry = InGameController.Instance?.ObjectDataWorker?.GetCharacterRegistry();
        if (registry == null)
            return null;

        var players = registry.GetAlivePlayers();
        PlayerCharacter nearest = null;
        float bestSqr = m_DetectRange * m_DetectRange;
        Vector3 pos = transform.position;

        foreach (var player in players)
        {
            if (player == null)
                continue;

            float sqr = (player.transform.position - pos).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                nearest = player;
            }
        }

        return nearest;
    }
}
