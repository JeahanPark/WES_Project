using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 WorldUI 관리 Worker
/// 풀링 및 위치 업데이트 담당
/// </summary>
public class InGameWorldUIWorker : MonoBehaviour
{
    private Camera m_Camera;
    private Transform m_WorldUIRoot;

    private Dictionary<Type, Queue<BaseWorldUI>> m_Pools = new();
    private Dictionary<Type, GameObject> m_Prefabs = new();
    private List<BaseWorldUI> m_ActiveWorldUIs = new();

    public void Initialize(Camera _camera)
    {
        m_Camera = _camera;
        m_WorldUIRoot = transform;
    }

    public void SetCamera(Camera _camera)
    {
        m_Camera = _camera;
    }

    public T CreateWorldUI<T>() where T : BaseWorldUI
    {
        Type type = typeof(T);
        T worldUI = GetFromPool<T>();

        if (worldUI == null)
        {
            worldUI = InstantiateWorldUI<T>();
        }

        if (worldUI == null)
        {
            GameDebug.LogError($"[InGameWorldUIWorker] Failed to create WorldUI: {type.Name}");
            return null;
        }

        worldUI.Initialize();
        m_ActiveWorldUIs.Add(worldUI);

        return worldUI;
    }

    public CharacterWorldUI CreateCharacterWorldUI(CharacterBase _character, Transform _target)
    {
        CharacterWorldUI worldUI = CreateWorldUI<CharacterWorldUI>();

        if (worldUI != null)
        {
            worldUI.SetTarget(_target, m_Camera);
            worldUI.SetCharacter(_character);
        }

        return worldUI;
    }

    public void ReleaseWorldUI(BaseWorldUI _worldUI)
    {
        if (_worldUI == null)
            return;

        m_ActiveWorldUIs.Remove(_worldUI);
        _worldUI.Release();

        ReturnToPool(_worldUI);
    }

    public void ReleaseAllWorldUIs()
    {
        for (int i = m_ActiveWorldUIs.Count - 1; i >= 0; i--)
        {
            BaseWorldUI worldUI = m_ActiveWorldUIs[i];
            if (worldUI != null)
            {
                worldUI.Release();
                ReturnToPool(worldUI);
            }
        }

        m_ActiveWorldUIs.Clear();
    }

    private void OnDestroy()
    {
        ClearAllPools();
    }

    private T GetFromPool<T>() where T : BaseWorldUI
    {
        Type type = typeof(T);

        if (!m_Pools.TryGetValue(type, out Queue<BaseWorldUI> pool))
            return null;

        if (pool.Count == 0)
            return null;

        return pool.Dequeue() as T;
    }

    private T InstantiateWorldUI<T>() where T : BaseWorldUI
    {
        Type type = typeof(T);

        if (!m_Prefabs.TryGetValue(type, out GameObject prefab))
        {
            // Addressable에서 로드 시도
            string prefabName = type.Name;
            GameObject loadedPrefab = Managers.Resource.LoadAddressable<GameObject>(prefabName);

            if (loadedPrefab == null)
            {
                GameDebug.LogError($"[InGameWorldUIWorker] Prefab not found: {prefabName}");
                return null;
            }

            m_Prefabs[type] = loadedPrefab;
            prefab = loadedPrefab;
        }

        GameObject instance = Instantiate(prefab, m_WorldUIRoot);
        return instance.GetComponent<T>();
    }

    private void ReturnToPool(BaseWorldUI _worldUI)
    {
        Type type = _worldUI.GetType();

        if (!m_Pools.TryGetValue(type, out Queue<BaseWorldUI> pool))
        {
            pool = new Queue<BaseWorldUI>();
            m_Pools[type] = pool;
        }

        pool.Enqueue(_worldUI);
    }

    private void ClearAllPools()
    {
        foreach (var pool in m_Pools.Values)
        {
            while (pool.Count > 0)
            {
                BaseWorldUI worldUI = pool.Dequeue();
                if (worldUI != null)
                {
                    Destroy(worldUI.gameObject);
                }
            }
        }

        m_Pools.Clear();
        m_Prefabs.Clear();
    }
}
