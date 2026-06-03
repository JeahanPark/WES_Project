using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Cold 실질화(추위 위협) 씬 와이어링 에디터 도구.
/// DayNightWorker가 붙은 GameObject에 ColdDamageWorker를 부착하고,
/// m_Config / m_DayNightWorker / InGameController.m_ColdDamageWorker 슬롯을 연결한다.
/// (ColdDamageWorker는 NetworkBehaviour이므로 DayNightWorker GameObject의 NetworkObject를 공유한다.)
/// </summary>
public static class ColdRealizationSetup
{
    private const string CONFIG_PATH = "Assets/GameResource/Config/DayNightConfig.asset";

    [MenuItem("WES/Tools/Setup Cold Realization")]
    public static void SetupColdRealization()
    {
        var config = AssetDatabase.LoadAssetAtPath<DayNightConfig>(CONFIG_PATH);
        if (config == null)
        {
            Debug.LogError("DayNightConfig asset not found at " + CONFIG_PATH + ". Run 'WES/Tools/Create DayNightConfig Asset' first.");
            return;
        }

        var dayNightWorker = Object.FindFirstObjectByType<DayNightWorker>();
        if (dayNightWorker == null)
        {
            Debug.LogError("DayNightWorker not found in scene. Run 'WES/Tools/Setup DayNightWorker Scene' first.");
            return;
        }

        var go = dayNightWorker.gameObject;

        if (go.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError("DayNightWorker GameObject has no NetworkObject. ColdDamageWorker (NetworkBehaviour) requires it. Re-run 'WES/Tools/Setup DayNightWorker Scene'.");
            return;
        }

        var coldWorker = go.GetComponent<ColdDamageWorker>();
        if (coldWorker == null)
            coldWorker = go.AddComponent<ColdDamageWorker>();

        WireSerialized(coldWorker, "m_Config", config);
        WireSerialized(coldWorker, "m_DayNightWorker", dayNightWorker);

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null)
        {
            Debug.LogError("InGameController not found in scene. Open Ingame scene first.");
            return;
        }
        WireSerialized(controller, "m_ColdDamageWorker", coldWorker);

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("Cold Realization setup complete: ColdDamageWorker added to DayNightWorker GameObject, m_Config / m_DayNightWorker / InGameController.m_ColdDamageWorker wired.");
    }

    private static void WireSerialized(Object _target, string _propName, Object _value)
    {
        if (_target == null) { Debug.LogError($"WireSerialized: target null for {_propName}"); return; }
        var so = new SerializedObject(_target);
        var prop = so.FindProperty(_propName);
        if (prop == null) { Debug.LogError($"Property '{_propName}' not found on {_target.GetType().Name}"); return; }
        prop.objectReferenceValue = _value;
        so.ApplyModifiedProperties();
    }
}
