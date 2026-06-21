using UnityEngine;

/// <summary>
/// 몬스터 추격 상태.
/// Perception 타깃을 향해 이동, AttackRange 진입 시 Attack 전이.
/// leash(스폰영역 반경 ×1.5) 초과·타깃 null·DetectRange 이탈 시 스폰지점 복귀(Walk).
/// Pack 몬스터는 진입 시 같은 area 동족에 타깃 전파.
/// </summary>
public class MonsterChaseState : MonsterStateBase
{
    public override void Enter()
    {
        m_StateMachine.Owner.StateAnimationComponent.PlayAnimation(AnimationType.Walk);

        // Pack(무리): 추격 시작을 같은 area 동족에 전파.
        var owner = m_StateMachine.Owner;
        if (owner.BehaviorType == MonsterBehaviorType.Pack && owner.Perception != null)
        {
            var target = owner.Perception.CurrentTarget;
            if (target != null && InGameController.Instance != null && InGameController.Instance.AreaWorker != null)
            {
                InGameController.Instance.AreaWorker.PropagatePackTarget(owner, owner.SpawnAreaId, target);
            }
        }
    }

    public override void Update()
    {
        var owner = m_StateMachine.Owner;
        var perception = owner.Perception;

        // 타깃 소실 / DetectRange 이탈 → 복귀.
        if (perception == null || !perception.HasTarget || perception.IsTargetOutOfDetectRange())
        {
            ReturnToSpawn();
            return;
        }

        // leash 초과 → 추격 포기, 스폰지점 복귀.
        if (m_StateMachine.DistanceFromSpawn() > owner.LeashRadius)
        {
            perception.ClearTarget();
            ReturnToSpawn();
            return;
        }

        var target = perception.CurrentTarget;

        // 사거리 진입 → 공격.
        if (m_StateMachine.DistanceTo(target.transform.position) <= owner.AttackRange)
        {
            m_StateMachine.ChangeState(MonsterStateType.Attack);
            return;
        }

        m_StateMachine.SetMoveTarget(target.transform.position);
        m_StateMachine.MoveTowardsTarget();
    }

    public override void Exit()
    {
    }

    private void ReturnToSpawn()
    {
        m_StateMachine.SetMoveTarget(m_StateMachine.SpawnPosition);
        m_StateMachine.ChangeState(MonsterStateType.Walk);
    }
}
