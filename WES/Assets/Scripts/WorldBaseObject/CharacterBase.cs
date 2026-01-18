using UnityEngine;

/// <summary>
/// "캐릭터" 공통 (플레이어/몬스터 둘 다 공유)
/// </summary>
public class CharacterBase : WorldEntityBase
{
    private const float MOVE_SPEED = 3.5f;
    private const float ROTATION_SPEED = 15f;

    [SerializeField] private CharacterAnimationComponent m_AnimationComponent;

    private Vector2 m_MoveDirection;
    private Vector3 m_LookTarget;
    private bool m_HasLookTarget;

    protected virtual void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        UpdateMovement();
        UpdateRotation();
    }

    public void MoveWithDirection(Vector2 _direction)
    {
        m_MoveDirection = _direction;
    }

    public void LookAtPosition(Vector3 _worldPosition)
    {
        m_LookTarget = _worldPosition;
        m_HasLookTarget = true;
    }

    public void Attack()
    {
        if (m_AnimationComponent == null)
            return;

        if (m_AnimationComponent.IsAttacking())
            return;
        m_AnimationComponent.PlayAttack();
    }

    private void UpdateMovement()
    {
        bool isMoving = m_MoveDirection.sqrMagnitude > 0f;

        if (m_AnimationComponent != null)
        {
            m_AnimationComponent.SetWalk(isMoving);
        }

        if (isMoving)
        {
            Vector3 moveDirection = new(m_MoveDirection.x, 0f, m_MoveDirection.y);
            transform.position += moveDirection.normalized * (Time.deltaTime * MOVE_SPEED);
        }
    }

    private void UpdateRotation()
    {
        if (!m_HasLookTarget)
            return;

        Vector3 lookDirection = m_LookTarget - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * ROTATION_SPEED);
        }
    }
}
