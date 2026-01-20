using UnityEngine;

/// <summary>
/// 캐릭터의 애니메이션을 담당하는 컴포넌트
/// </summary>
public class CharacterAnimationComponent : MonoBehaviour
{
    private const string ATTACK_STATE_NAME = "Attack";
    private const string INTERACT_STATE_NAME = "Interact";
    private const int BASE_LAYER = 0;
    private const int UPPER_BODY_LAYER = 1;

    private static readonly int HASH_WALK = Animator.StringToHash("Walk");
    private static readonly int HASH_ATTACK = Animator.StringToHash("Attack");
    private static readonly int HASH_INTERACT = Animator.StringToHash("Interact");

    [SerializeField] private Animator m_Animator;

    private bool m_IsWalking;

    public void Initialize(Animator _animator)
    {
        m_Animator = _animator;
    }

    public bool IsAttacking()
    {
        if (m_Animator == null)
            return false;

        // 현재 상태 + 전이중인 다음 상태 둘 다 체크
        if (m_Animator.IsInTransition(UPPER_BODY_LAYER))
        {
            var next = m_Animator.GetNextAnimatorStateInfo(UPPER_BODY_LAYER);
            if (next.IsName(ATTACK_STATE_NAME))
                return true;
        }

        var cur = m_Animator.GetCurrentAnimatorStateInfo(UPPER_BODY_LAYER);
        return cur.IsName(ATTACK_STATE_NAME);
    }

    public void SetWalk(bool _isWalking)
    {
        if (m_Animator == null)
            return;

        if (m_IsWalking == _isWalking)
            return;

        m_IsWalking = _isWalking;
        m_Animator.SetBool(HASH_WALK, m_IsWalking);
    }

    public void PlayAttack()
    {
        if (m_Animator == null)
            return;

        m_Animator.SetTrigger(HASH_ATTACK);
    }

    public bool IsInteracting()
    {
        if (m_Animator == null)
            return false;

        if (m_Animator.IsInTransition(BASE_LAYER))
        {
            var next = m_Animator.GetNextAnimatorStateInfo(BASE_LAYER);
            if (next.IsName(INTERACT_STATE_NAME))
                return true;
        }

        var cur = m_Animator.GetCurrentAnimatorStateInfo(BASE_LAYER);
        return cur.IsName(INTERACT_STATE_NAME);
    }

    public void PlayInteract()
    {
        if (m_Animator == null)
            return;

        m_Animator.SetTrigger(HASH_INTERACT);
    }
}
