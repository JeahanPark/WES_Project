using UnityEngine;

/// <summary>
/// 몬스터 대기 상태
/// </summary>
public class MonsterIdleState : MonsterStateBase
{
    private const float IDLE_DURATION = 3f;

    private float m_Timer;

    public override void Enter()
    {
        m_StateMachine.Owner.StateAnimationComponent.PlayAnimation(AnimationType.Idle);
        m_Timer = 0f;
    }

    public override void Update()
    {
        m_Timer += Time.deltaTime;

        if (m_Timer >= IDLE_DURATION)
        {
            m_StateMachine.SetRandomTarget();
            m_StateMachine.ChangeState(MonsterStateType.Walk);
        }
    }

    public override void Exit()
    {
    }
}
