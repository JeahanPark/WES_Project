/// <summary>
/// 몬스터 상태의 기본 클래스
/// </summary>
public abstract class MonsterStateBase
{
    protected MonsterStateMachine m_StateMachine;

    public void Initialize(MonsterStateMachine _stateMachine)
    {
        m_StateMachine = _stateMachine;
    }

    public abstract void Enter();
    public abstract void Update();
    public abstract void Exit();
}
