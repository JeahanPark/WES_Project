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
    }

    protected virtual void OnDestroy()
    {
        if (s_Instance == this)
        {
            Clear();
            s_Instance = null;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        if (s_Instance == this)
        {
            s_Instance = null;
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

    public override void Init()
    {
        base.Init();
        InitializeManagers();
    }

    private void InitializeManagers()
    {
        // 각 매니저 명시적 초기화
        Camera.Init();
        Input.Init();
        Info.Init();
        Game.Init();
        User.Init();
        Network.Init();
        Popup.Init();
        Resource.Init();
    }

    public override void Clear()
    {
        base.Clear();
        ClearManagers();
    }

    private void ClearManagers()
    {
        // 각 매니저 정리
        if (Camera) Camera.Clear();
        if (Input) Input.Clear();
        Info?.Clear();
        Game?.Clear();
        User?.Clear();
        if (Network) Network.Clear();
        if (Popup) Popup.Clear();
        Resource?.Clear();
    }
}
