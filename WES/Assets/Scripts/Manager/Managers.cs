using UnityEngine;

public interface IManager
{
    void Init();
    void Clear();
}

public abstract class MonoSingleton<T> : MonoBehaviour, IManager where T : MonoBehaviour
{
    private static T s_Instance;

    public static T Instance
    {
        get
        {
            if (s_Instance == null)
            {
                s_Instance = FindFirstObjectByType<T>();
                if (s_Instance == null)
                {
                    GameObject go = new GameObject(typeof(T).Name);
                    s_Instance = go.AddComponent<T>();
                }
            }
            return s_Instance;
        }
    }

    protected virtual void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this as T;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    protected virtual void OnDestroy()
    {
        if (s_Instance == this)
        {
            Clear();
        }
    }

    public virtual void Init()
    {
    }

    public virtual void Clear()
    {
    }
}

public abstract class Singleton<T> : IManager where T : class, new()
{
    private static T s_Instance;

    public static T Instance
    {
        get
        {
            if (s_Instance == null)
            {
                s_Instance = new T();
                if (s_Instance is IManager manager)
                {
                    manager.Init();
                }
            }
            return s_Instance;
        }
    }

    public virtual void Init()
    {
    }

    public virtual void Clear()
    {
        s_Instance = null;
    }
}

public class Managers : MonoSingleton<Managers>
{
    public static CameraManager Camera => CameraManager.Instance;
    public static InputManager Input => InputManager.Instance;
    public static InfoManager Info => InfoManager.Instance;
    public static GameManager Game => GameManager.Instance;
    public static UserManager User => UserManager.Instance;
    public static GameNetworkManager Network => GameNetworkManager.Instance;
    public static PopupManager Popup => PopupManager.Instance;
    public static ResourceManager Resource => ResourceManager.Instance;
}
