using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkObject 스폰/디스폰 및 오브젝트 풀링을 전담하는 Worker.
/// 서버에서만 실행되며, 다른 Worker가 이 Worker에 스폰 요청을 한다.
/// </summary>
public class InGameSpawnWorker : MonoBehaviour
{
    private Dictionary<string, Queue<NetworkObject>> m_Pool = new();

    /// <summary>
    /// 오브젝트 스폰 (풀에서 꺼내거나 새로 생성). 서버에서만 호출해야 한다.
    /// </summary>
    public T SpawnObject<T>(string _prefabKey, Vector3 _position) where T : NetworkBehaviour
    {
        var networkObj = SpawnObject(_prefabKey, _position);
        return networkObj != null ? networkObj.GetComponent<T>() : null;
    }

    /// <summary>
    /// 오브젝트 스폰 (NetworkObject 반환). 서버에서만 호출해야 한다.
    /// </summary>
    public NetworkObject SpawnObject(string _prefabKey, Vector3 _position)
    {
        if (!Managers.Network.IsServer)
        {
            GameDebug.LogError("[InGameSpawnWorker] SpawnObject must be called on server only.");
            return null;
        }

        NetworkObject obj = GetFromPool(_prefabKey);

        if (obj == null)
        {
            GameObject prefab = Managers.Resource.LoadAddressable<GameObject>(_prefabKey);
            if (prefab == null)
            {
                GameDebug.LogError($"[InGameSpawnWorker] Prefab not found: {_prefabKey}");
                return null;
            }

            obj = Instantiate(prefab, _position, Quaternion.identity).GetComponent<NetworkObject>();
            if (obj == null)
            {
                GameDebug.LogError($"[InGameSpawnWorker] NetworkObject component not found on prefab: {_prefabKey}");
                return null;
            }
        }
        else
        {
            obj.transform.position = _position;
        }

        obj.Spawn();
        GameDebug.Log($"[InGameSpawnWorker] Spawned: {_prefabKey} at {_position}");
        return obj;
    }

    /// <summary>
    /// 오브젝트 디스폰 후 풀에 반납.
    /// 서버에서만 호출해야 한다.
    /// </summary>
    public void DespawnObject(NetworkObject _obj, string _prefabKey)
    {
        if (!Managers.Network.IsServer)
        {
            GameDebug.LogError("[InGameSpawnWorker] DespawnObject must be called on server only.");
            return;
        }

        if (_obj == null)
        {
            GameDebug.LogWarning("[InGameSpawnWorker] DespawnObject called with null object.");
            return;
        }

        _obj.Despawn(false);

        if (!m_Pool.ContainsKey(_prefabKey))
            m_Pool[_prefabKey] = new Queue<NetworkObject>();

        m_Pool[_prefabKey].Enqueue(_obj);
        GameDebug.Log($"[InGameSpawnWorker] Despawned and pooled: {_prefabKey}");
    }

    private NetworkObject GetFromPool(string _prefabKey)
    {
        if (m_Pool.TryGetValue(_prefabKey, out var queue) && queue.Count > 0)
            return queue.Dequeue();

        return null;
    }
}
