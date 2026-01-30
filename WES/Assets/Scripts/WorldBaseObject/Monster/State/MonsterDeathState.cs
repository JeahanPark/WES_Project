using UnityEngine;

/// <summary>
/// 몬스터 사망 상태
/// </summary>
public class MonsterDeathState : MonsterStateBase
{
    private const float DEATH_DURATION = 2f;

    private float m_Timer;

    public override void Enter()
    {
        m_StateMachine.Owner.StateAnimationComponent.PlayAnimation(AnimationType.Death);
        m_StateMachine.SetCollisionEnabled(false);
        m_Timer = 0f;
    }

    public override void Update()
    {
        m_Timer += Time.deltaTime;

        if (m_Timer >= DEATH_DURATION)
        {
            m_StateMachine.RequestDespawn();
        }
    }

    public override void Exit()
    {
    }
}
