using UnityEngine;

/// <summary>
/// 몬스터 피격 상태
/// </summary>
public class MonsterHitState : MonsterStateBase
{
    private const float HIT_STUN_DURATION = 0.5f;

    private float m_Timer;

    public override void Enter()
    {
        m_StateMachine.Owner.SetHitColor(true);
        m_Timer = 0f;
    }

    public override void Update()
    {
        m_Timer += Time.deltaTime;

        if (m_Timer >= HIT_STUN_DURATION)
        {
            m_StateMachine.ChangeState(MonsterStateType.Idle);
        }
    }

    public override void Exit()
    {
        m_StateMachine.Owner.SetHitColor(false);
    }
}
