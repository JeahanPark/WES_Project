using UnityEngine;

/// <summary>
/// 애니메이션 컴포넌트의 기본 클래스
/// </summary>
public abstract class GameAnimationComponent : MonoBehaviour
{
    [SerializeField] private Animator m_Animator;

    protected Animator Animator => m_Animator;

    public void Initialize(Animator _animator)
    {
        m_Animator = _animator;
    }
}
