using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceManager : Singleton<ResourceManager>
{
    public T Load<T>(string _path) where T : Object
    {
        return Resources.Load<T>(_path);
    }

    public GameObject Instantiate(string _path, Transform _parent = null)
    {
        GameObject prefab = Load<GameObject>(_path);
        if (prefab == null)
        {
            GameDebug.LogError($"Failed to load prefab at path: {_path}");
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, _parent);
        instance.name = prefab.name;
        return instance;
    }

    public T LoadAddressable<T>(string _key) where T : Object
    {
        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(_key);
        handle.WaitForCompletion();

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            return handle.Result;
        }
        else
        {
            GameDebug.LogError($"Failed to load addressable asset with key: {_key}");
            return null;
        }
    }

    public GameObject InstantiateAddressable(string _key, Transform _parent = null)
    {
        GameObject prefab = LoadAddressable<GameObject>(_key);
        if (prefab == null)
        {
            GameDebug.LogError($"Failed to load addressable prefab with key: {_key}");
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, _parent);
        instance.name = prefab.name;
        return instance;
    }

    public void Destroy(GameObject _obj, float _delay = 0f)
    {
        if (_obj == null)
        {
            return;
        }

        if (_delay > 0f)
        {
            Object.Destroy(_obj, _delay);
        }
        else
        {
            Object.Destroy(_obj);
        }
    }
}
