using UnityEngine;

/// <summary>
/// "캐릭터" 공통 (플레이어/몬스터 둘 다 공유)
/// </summary>
public class CharacterBase : WorldEntityBase
{
    private Vector2 m_MoveDirection;

    protected virtual void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        UpdateMovement();
    }

    public void MoveWithDirection(Vector2 _direction)
    {
        m_MoveDirection = _direction;
    }

    private void UpdateMovement()
    {
        if (m_MoveDirection.sqrMagnitude > 0f)
        {
            Vector3 moveDirection = new(m_MoveDirection.x, 0f, m_MoveDirection.y);
            transform.position += moveDirection.normalized * (Time.deltaTime * 3.5f);

            // 이동 방향으로 회전
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
    }
}
