using UnityEngine;

/// <summary>
/// 플레이어 캐릭터의 애니메이션을 담당하는 컴포넌트
/// 상하체 분리 애니메이션을 위해 파라미터 방식 사용
/// </summary>
public class PlayerAnimationComponent : GameAnimationComponent
{
    private const string ATTACK_STATE_NAME = "Attack";
    private const string INTERACT_STATE_NAME = "Interact";
    private const int BASE_LAYER = 0;
    private const int UPPER_BODY_LAYER = 1;
    private const float DEFAULT_ATTACK_RANGE = 1.5f;
    private const int DEFAULT_ATTACK_DAMAGE = 10;
    private const int DEFAULT_MAX_HIT_COUNT = 3;

    private static readonly int HASH_WALK = Animator.StringToHash("Walk");
    private static readonly int HASH_ATTACK = Animator.StringToHash("Attack");
    private static readonly int HASH_INTERACT = Animator.StringToHash("Interact");

    [SerializeField] private float m_AttackRange = DEFAULT_ATTACK_RANGE;
    [SerializeField] private int m_AttackDamage = DEFAULT_ATTACK_DAMAGE;
    [SerializeField] private int m_MaxHitCount = DEFAULT_MAX_HIT_COUNT;
    [SerializeField] private LayerMask m_TargetLayer;

    private CharacterBase m_Owner;
    private bool m_IsWalking;

    private void Awake()
    {
        m_Owner = GetComponentInParent<CharacterBase>();
    }

    public bool IsAttacking()
    {
        if (Animator == null)
            return false;

        if (Animator.IsInTransition(UPPER_BODY_LAYER))
        {
            var next = Animator.GetNextAnimatorStateInfo(UPPER_BODY_LAYER);
            if (next.IsName(ATTACK_STATE_NAME))
                return true;
        }

        var cur = Animator.GetCurrentAnimatorStateInfo(UPPER_BODY_LAYER);
        return cur.IsName(ATTACK_STATE_NAME);
    }

    public void SetWalk(bool _isWalking)
    {
        if (Animator == null)
            return;

        if (m_IsWalking == _isWalking)
            return;

        m_IsWalking = _isWalking;
        Animator.SetBool(HASH_WALK, m_IsWalking);
    }

    public void PlayAttack()
    {
        if (Animator == null)
            return;

        Animator.SetTrigger(HASH_ATTACK);
    }

    public bool IsInteracting()
    {
        if (Animator == null)
            return false;

        if (Animator.IsInTransition(BASE_LAYER))
        {
            var next = Animator.GetNextAnimatorStateInfo(BASE_LAYER);
            if (next.IsName(INTERACT_STATE_NAME))
                return true;
        }

        var cur = Animator.GetCurrentAnimatorStateInfo(BASE_LAYER);
        return cur.IsName(INTERACT_STATE_NAME);
    }

    public void PlayInteract()
    {
        if (Animator == null)
            return;

        Animator.SetTrigger(HASH_INTERACT);
    }

    /// <summary>
    /// Animation Event에서 호출 - 공격 판정 수행
    /// </summary>
    public void OnAnimEvent_Attack()
    {
        if (m_Owner == null || !m_Owner.IsOwner)
            return;

        if (InGameController.Instance == null || InGameController.Instance.ColliderWorker == null)
            return;

        Vector3 attackPosition = m_Owner.transform.position + m_Owner.transform.forward * (m_AttackRange * 0.5f);

        InGameController.Instance.ColliderWorker.CreateCollider(
            m_Owner,
            attackPosition,
            m_AttackRange,
            m_AttackDamage,
            m_MaxHitCount,
            m_TargetLayer
        );
    }
}
