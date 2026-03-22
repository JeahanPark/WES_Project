using UnityEngine;

/// <summary>
/// 씬에 배치되는 몬스터 스폰 구역.
/// AreaId로 WorldAreaInfo를 참조하며, 스폰 위치를 제공한다.
/// </summary>
public class MonsterSpawnArea : MonoBehaviour
{
    [SerializeField] private int m_AreaId;
    [SerializeField] private float m_SpawnRadius = 5f;

    public int AreaId => m_AreaId;

    public Vector3 GetRandomSpawnPosition()
    {
        Vector2 random = Random.insideUnitCircle * m_SpawnRadius;
        return transform.position + new Vector3(random.x, 0f, random.y);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, m_SpawnRadius);
    }
#endif
}
