using UnityEngine;

/// <summary>
/// 몬스터 기본 클래스
/// </summary>
public abstract class MonsterBase : CharacterBase
{
    [SerializeField] private StateAnimationComponent m_StateAnimationComponent;
    [SerializeField] private MonsterStateMachine m_StateMachine;

    public StateAnimationComponent StateAnimationComponent => m_StateAnimationComponent;

    protected override void OnDamaged(int _damage, CharacterBase _attacker)
    {
        base.OnDamaged(_damage, _attacker);

        if (!IsDead && m_StateMachine != null)
        {
            m_StateMachine.ChangeState(MonsterStateType.Hit);
        }
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        if (m_StateMachine != null)
        {
            m_StateMachine.ChangeState(MonsterStateType.Death);
        }
    }
}
