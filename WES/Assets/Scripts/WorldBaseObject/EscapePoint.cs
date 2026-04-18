using UnityEngine;

/// <summary>
/// 탈출 지점 컴포넌트
/// - 플레이어가 트리거 진입 시 InGameController에 탈출 보고
/// </summary>
public class EscapePoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider _other)
    {
        PlayerCharacter player = _other.GetComponentInParent<PlayerCharacter>();
        if (player == null)
            return;

        if (!player.IsOwner)
            return;

        InGameController.Instance?.OnPlayerReachedEscape(player);
    }
}
