using UnityEngine;

public class TestCode : MonoBehaviour
{
    [SerializeField] private float m_Radius = 1.5f;
    [SerializeField] private Color m_GizmoColor = Color.yellow;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"Test Gizmo at {transform.position}");
            Debug.DrawRay(transform.position, Vector3.up * 2f, Color.red, 1f);
            Debug.DrawRay(transform.position, Vector3.forward * m_Radius, Color.green, 1f);
            Debug.DrawRay(transform.position, Vector3.right * m_Radius, Color.blue, 1f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = m_GizmoColor;
        Gizmos.DrawWireSphere(transform.position, m_Radius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_Radius * 1.1f);
    }
}
