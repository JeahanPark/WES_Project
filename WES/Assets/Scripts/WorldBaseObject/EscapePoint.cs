using UnityEngine;

/// <summary>
/// 탈출 지점 컴포넌트
/// - 플레이어가 트리거 진입 시 InGameController에 탈출 보고
/// </summary>
public class EscapePoint : MonoBehaviour
{
    private static readonly Color GIZMO_FILL = new Color(0f, 1f, 0f, 0.25f);
    private static readonly Color GIZMO_WIRE = new Color(0f, 1f, 0.2f, 1f);

    private void OnTriggerEnter(Collider _other)
    {
        PlayerCharacter player = _other.GetComponentInParent<PlayerCharacter>();
        if (player == null)
            return;

        if (!player.IsOwner)
            return;

        InGameController.Instance?.OnPlayerReachedEscape(player);
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Vector3 center = box != null ? box.center : Vector3.zero;
        Vector3 size = box != null ? box.size : Vector3.one;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = GIZMO_FILL;
        Gizmos.DrawCube(center, size);
        Gizmos.color = GIZMO_WIRE;
        Gizmos.DrawWireCube(center, size);

        // 위쪽 방향 화살표 — 멀리서도 식별
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = GIZMO_WIRE;
        Vector3 top = transform.position + Vector3.up * (size.y * 0.5f);
        Gizmos.DrawLine(top, top + Vector3.up * 4f);
        Gizmos.DrawWireSphere(top + Vector3.up * 4f, 0.5f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = GIZMO_WIRE;
        UnityEditor.Handles.Label(top + Vector3.up * 4.7f, "EscapePoint");
#endif
    }
}
