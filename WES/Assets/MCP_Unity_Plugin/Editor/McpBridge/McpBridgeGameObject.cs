// McpBridgeGameObject.cs
// MCP Bridge 핸들러: add_gameobject
// 씬 또는 프리팹의 특정 GameObject 하위에 새 빈 GameObject를 추가한다.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class McpBridge
{
    private static string AddGameObject(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.target))
            return BuildError("'target' (parentTarget) is required");

        if (string.IsNullOrEmpty(_req.gameObjectName))
            return BuildError("'gameObjectName' is required");

        // 프리팹 모드: LoadPrefabContents로 격리 편집 후 저장
        if (!string.IsNullOrEmpty(_req.prefabPath))
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(_req.prefabPath);
            if (prefabContents == null)
                return BuildError($"Failed to load prefab: '{_req.prefabPath}'");

            try
            {
                GameObject parentGo;
                if (string.IsNullOrEmpty(_req.target) || _req.target == prefabContents.name)
                    parentGo = prefabContents;
                else
                    parentGo = FindInHierarchy(prefabContents.transform, _req.target)?.gameObject;

                if (parentGo == null)
                    return BuildError($"Parent GameObject not found: '{_req.target}'");

                var newGo = new GameObject(_req.gameObjectName);
                newGo.transform.SetParent(parentGo.transform, false);

                PrefabUtility.SaveAsPrefabAsset(prefabContents, _req.prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            return BuildSuccess($"GameObject '{_req.gameObjectName}' added under '{_req.target}'");
        }

        // 씬 모드
        var (sceneParent, _) = FindTarget(_req);
        if (sceneParent == null)
            return BuildError($"Parent GameObject not found: '{_req.target}'");

        var newSceneGo = new GameObject(_req.gameObjectName);
        Undo.RegisterCreatedObjectUndo(newSceneGo, "MCP Add GameObject");
        newSceneGo.transform.SetParent(sceneParent.transform, false);

        SaveTarget(newSceneGo, null);
        return BuildSuccess($"GameObject '{_req.gameObjectName}' added under '{_req.target}'");
    }
}
