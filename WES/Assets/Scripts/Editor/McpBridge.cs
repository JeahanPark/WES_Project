// McpBridge.cs
// Unity Editor TCP 브릿지 코어 - TCP 서버 생명주기, 요청 라우팅, 공용 헬퍼.
// 기능별 핸들러는 McpBridge/ 하위 partial 파일에 분리되어 있다.
// Tools > McpBridge 메뉴에서 수동으로 시작/중지한다.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class McpBridge
{
    private const int PORT = 9876;

    private static TcpListener m_Listener;
    private static Thread m_ListenThread;
    private static bool m_Running;
    private static readonly ConcurrentQueue<PendingRequest> m_PendingQueue = new ConcurrentQueue<PendingRequest>();

    // ---- Tools 메뉴 ----

    [MenuItem("Tools/McpBridge/▶ 시작", priority = 1)]
    public static void MenuStart()
    {
        Start();
    }

    [MenuItem("Tools/McpBridge/▶ 시작", isValidateFunction: true)]
    private static bool MenuStartValidate() => !m_Running;

    [MenuItem("Tools/McpBridge/■ 중지", priority = 2)]
    public static void MenuStop()
    {
        Stop();
    }

    [MenuItem("Tools/McpBridge/■ 중지", isValidateFunction: true)]
    private static bool MenuStopValidate() => m_Running;

    // ---- 서버 생명주기 ----

    private static void Start()
    {
        try
        {
            m_Running = true;
            m_Listener = new TcpListener(IPAddress.Loopback, PORT);
            m_Listener.Start();
            m_ListenThread = new Thread(ListenLoop) { IsBackground = true, Name = "McpBridge" };
            m_ListenThread.Start();
            EditorApplication.update += ProcessQueue;
            EditorApplication.quitting += Stop;
            Debug.Log($"[McpBridge] TCP 서버 시작됨 (port {PORT})");
        }
        catch (Exception e)
        {
            m_Running = false;
            Debug.LogError($"[McpBridge] 서버 시작 실패: {e.Message}");
        }
    }

    private static void Stop()
    {
        m_Running = false;
        m_Listener?.Stop();
        EditorApplication.update -= ProcessQueue;
        EditorApplication.quitting -= Stop;
        Debug.Log("[McpBridge] TCP 서버 중지됨");
    }

    private static void ListenLoop()
    {
        while (m_Running)
        {
            try
            {
                var client = m_Listener.AcceptTcpClient();
                var t = new Thread(() => HandleClient(client)) { IsBackground = true };
                t.Start();
            }
            catch
            {
                break;
            }
        }
    }

    // ---- 클라이언트 처리 ----

    private static void HandleClient(TcpClient _client)
    {
        using (_client)
        {
            try
            {
                using var stream = _client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                string json = reader.ReadLine();
                if (string.IsNullOrEmpty(json))
                    return;

                var pending = new PendingRequest(json);
                m_PendingQueue.Enqueue(pending);
                pending.WaitForCompletion(10000);

                writer.WriteLine(pending.Response ?? BuildError("timeout"));
            }
            catch (Exception e)
            {
                Debug.LogError($"[McpBridge] 클라이언트 처리 오류: {e.Message}");
            }
        }
    }

    // EditorApplication.update 에서 메인 스레드로 커맨드 처리
    private static void ProcessQueue()
    {
        while (m_PendingQueue.TryDequeue(out var req))
        {
            try
            {
                req.Response = HandleRequest(req.Json);
            }
            catch (Exception e)
            {
                req.Response = BuildError(e.Message);
            }
            finally
            {
                req.Signal();
            }
        }
    }

    // ---- 요청 라우팅 ----

    private static string HandleRequest(string _json)
    {
        var req = JsonUtility.FromJson<BridgeRequest>(_json);
        if (req == null)
            return BuildError("Invalid JSON");
        if (string.IsNullOrEmpty(req.action))
            return BuildError("'action' is required");

        return req.action.ToLowerInvariant() switch
        {
            "add"                => AddComponent(req),       // McpBridgeComponents.cs
            "remove"             => RemoveComponent(req),    // McpBridgeComponents.cs
            "set_property"       => SetProperty(req),        // McpBridgeComponents.cs
            "list"               => ListComponents(req),     // McpBridgeComponents.cs
            "set_reference"      => SetReference(req),       // McpBridgeReferences.cs
            "instantiate_prefab" => InstantiatePrefab(req),  // McpBridgeInstantiate.cs
            _                    => BuildError($"Unknown action: '{req.action}'")
        };
    }

    // ---- 공용 헬퍼 ----

    private static (GameObject go, GameObject prefabRoot) FindTarget(BridgeRequest _req)
    {
        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_req.prefabPath);
            if (prefab == null)
                return (null, null);

            if (string.IsNullOrEmpty(_req.target) || _req.target == prefab.name)
                return (prefab, prefab);

            var found = FindInHierarchy(prefab.transform, _req.target);
            return (found, prefab);
        }

        foreach (var root in GetAllSceneRoots())
        {
            if (root.name == _req.target)
                return (root, null);

            var found = FindInHierarchy(root.transform, _req.target);
            if (found != null)
                return (found, null);
        }

        return (null, null);
    }

    private static GameObject FindInHierarchy(Transform _parent, string _name)
    {
        foreach (Transform child in _parent)
        {
            if (child.name == _name)
                return child.gameObject;

            var found = FindInHierarchy(child, _name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static GameObject[] GetAllSceneRoots()
    {
        var result = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                result.AddRange(scene.GetRootGameObjects());
        }
        return result.ToArray();
    }

    private static void SaveTarget(GameObject _go, GameObject _prefabRoot)
    {
        if (_prefabRoot != null)
        {
            EditorUtility.SetDirty(_prefabRoot);
            PrefabUtility.SavePrefabAsset(_prefabRoot);
            AssetDatabase.SaveAssets();
        }
        else
        {
            EditorUtility.SetDirty(_go);
            EditorSceneManager.MarkSceneDirty(_go.scene);
        }
    }

    private static Type FindComponentType(string _typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = assembly.GetTypes().FirstOrDefault(x =>
                (x.Name == _typeName || x.FullName == _typeName) &&
                typeof(Component).IsAssignableFrom(x));
            if (t != null)
                return t;
        }
        return null;
    }

    private static string BuildSuccess(string _message) =>
        $"{{\"success\":true,\"message\":\"{Escape(_message)}\"}}";

    private static string BuildError(string _message) =>
        $"{{\"success\":false,\"message\":\"{Escape(_message)}\"}}";

    private static string Escape(string _s) =>
        _s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";

    // ---- DTO ----

    [Serializable]
    private class BridgeRequest
    {
        public string action;
        public string target;
        public string prefabPath;
        public string parentPrefabPath;
        public string componentType;
        public string propertyName;
        public string propertyValue;
        public string mappingsJson;
    }

    private class PendingRequest
    {
        private readonly ManualResetEventSlim m_Done = new ManualResetEventSlim(false);

        public string Json     { get; }
        public string Response { get; set; }

        public PendingRequest(string _json) { Json = _json; }

        public void Signal()                        => m_Done.Set();
        public void WaitForCompletion(int _timeout) => m_Done.Wait(_timeout);
    }
}
