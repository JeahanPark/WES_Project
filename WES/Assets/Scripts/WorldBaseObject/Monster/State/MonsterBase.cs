using UnityEngine;

/// <summary>
/// 몬스터 기본 클래스
/// </summary>
public abstract class MonsterBase : CharacterBase
{
    [SerializeField] private StateAnimationComponent m_StateAnimationComponent;
    [SerializeField] private MonsterStateMachine m_StateMachine;
    [SerializeField] private Renderer m_Renderer;

    private Color m_OriginalColor;

    public StateAnimationComponent StateAnimationComponent => m_StateAnimationComponent;

    protected virtual void Awake()
    {
        if (m_Renderer != null)
        {
            m_OriginalColor = m_Renderer.material.color;
        }
    }

    public void SetHitColor(bool _isHit)
    {
        if (m_Renderer == null)
            return;

        m_Renderer.material.color = _isHit ? Color.red : m_OriginalColor;
    }

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
