using UnityEngine;

/// <summary>
/// 몬스터 이동 상태
/// </summary>
public class MonsterWalkState : MonsterStateBase
{
    private const float ARRIVAL_THRESHOLD = 0.5f;

    public override void Enter()
    {
        m_StateMachine.Owner.StateAnimationComponent.PlayAnimation(AnimationType.Walk);
    }

    public override void Update()
    {
        if (m_StateMachine.HasReachedTarget(ARRIVAL_THRESHOLD))
        {
            m_StateMachine.ChangeState(MonsterStateType.Idle);
            return;
        }

        m_StateMachine.MoveTowardsTarget();
    }

    public override void Exit()
    {
    }
}
