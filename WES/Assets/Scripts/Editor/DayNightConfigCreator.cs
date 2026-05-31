using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public static class DayNightConfigCreator
{
    private const string CONFIG_FOLDER = "Assets/GameResource/Config";
    private const string CONFIG_PATH = "Assets/GameResource/Config/DayNightConfig.asset";

    [MenuItem("WES/Tools/Convert Modified CSVs")]
    public static void ConvertModifiedCSVs()
    {
        var paths = new[] {
            "Assets/CSVInfo/WorldAreaMonsterInfo.csv",
            "Assets/CSVInfo/MonsterInfo.csv",
            "Assets/CSVInfo/DropTableItemInfo.csv",
        };
        foreach (var p in paths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(p);
            if (asset == null) { Debug.LogError("CSV not found: " + p); continue; }
            Selection.activeObject = asset;
            EditorApplication.ExecuteMenuItem("Assets/InfoConvert");
            Debug.Log("Converted: " + p);
        }
        AssetDatabase.Refresh();
    }

    [MenuItem("WES/Tools/Setup Slice2 Scene")]
    public static void SetupSlice2Scene()
    {
        var config = AssetDatabase.LoadAssetAtPath<DayNightConfig>(CONFIG_PATH);
        if (config == null) { Debug.LogError("DayNightConfig not found"); return; }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null) { Debug.LogError("InGameController not found"); return; }

        // 1) DayNightRenderWorker (루트)
        var renderObj = GameObject.Find("DayNightRenderWorker");
        if (renderObj == null) renderObj = new GameObject("DayNightRenderWorker");
        else renderObj.transform.SetParent(null);
        var renderWorker = renderObj.GetComponent<DayNightRenderWorker>() ?? renderObj.AddComponent<DayNightRenderWorker>();
        var dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
        WireSerialized(renderWorker, "m_Config", config);
        if (dirLight != null) WireSerialized(renderWorker, "m_DirectionalLight", dirLight);
        WireSerialized(controller, "m_DayNightRenderWorker", renderWorker);

        // 2) NightVisionRoot 트리 (Canvas 하위)
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("Canvas not found"); return; }

        var nightVisionRoot = canvas.transform.Find("NightVisionRoot")?.gameObject;
        if (nightVisionRoot == null)
        {
            nightVisionRoot = new GameObject("NightVisionRoot", typeof(RectTransform));
            nightVisionRoot.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)nightVisionRoot.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        var darkness = nightVisionRoot.transform.Find("DarknessOverlay")?.GetComponent<Image>();
        if (darkness == null)
        {
            var dgo = new GameObject("DarknessOverlay", typeof(RectTransform), typeof(Image));
            dgo.transform.SetParent(nightVisionRoot.transform, false);
            var drt = (RectTransform)dgo.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            darkness = dgo.GetComponent<Image>();
            darkness.color = new Color(0f, 0f, 0f, 0f);
            darkness.raycastTarget = false;
        }

        var lightContainer = nightVisionRoot.transform.Find("LightSourceContainer") as RectTransform;
        if (lightContainer == null)
        {
            var lgo = new GameObject("LightSourceContainer", typeof(RectTransform));
            lgo.transform.SetParent(nightVisionRoot.transform, false);
            lightContainer = (RectTransform)lgo.transform;
            lightContainer.anchorMin = Vector2.zero; lightContainer.anchorMax = Vector2.one;
            lightContainer.offsetMin = Vector2.zero; lightContainer.offsetMax = Vector2.zero;
        }

        var template = nightVisionRoot.transform.Find("LightCircleTemplate")?.GetComponent<Image>();
        if (template == null)
        {
            var tgo = new GameObject("LightCircleTemplate", typeof(RectTransform), typeof(Image));
            tgo.transform.SetParent(nightVisionRoot.transform, false);
            tgo.SetActive(false);
            template = tgo.GetComponent<Image>();
            template.color = new Color(1f, 0.9f, 0.6f, 0.5f);
            template.raycastTarget = false;
            var trt = (RectTransform)tgo.transform;
            trt.sizeDelta = new Vector2(100f, 100f);
        }

        var nightVision = nightVisionRoot.GetComponent<NightVisionComponent>() ?? nightVisionRoot.AddComponent<NightVisionComponent>();
        WireSerialized(nightVision, "m_Config", config);
        WireSerialized(nightVision, "m_DarknessOverlay", darkness);
        WireSerialized(nightVision, "m_LightSourceContainer", lightContainer);
        WireSerialized(nightVision, "m_LightCirclePrefab", template);

        // 3) PhaseIconHUD (Canvas/InGameHUDWorker 하위)
        var hudWorker = GameObject.Find("Canvas/InGameHUDWorker");
        if (hudWorker == null) { Debug.LogError("InGameHUDWorker GameObject not found"); return; }
        var hudComp = hudWorker.GetComponent<InGameHUDWorker>();

        var phaseIconObj = hudWorker.transform.Find("PhaseIconHUD")?.gameObject;
        if (phaseIconObj == null)
        {
            phaseIconObj = new GameObject("PhaseIconHUD", typeof(RectTransform));
            phaseIconObj.transform.SetParent(hudWorker.transform, false);
            var prt = (RectTransform)phaseIconObj.transform;
            prt.anchorMin = new Vector2(1f, 1f); prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-20f, -20f);
            prt.sizeDelta = new Vector2(64f, 64f);
        }
        var iconImage = phaseIconObj.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage == null)
        {
            var igo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            igo.transform.SetParent(phaseIconObj.transform, false);
            var irt = (RectTransform)igo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            iconImage = igo.GetComponent<Image>();
            iconImage.raycastTarget = false;
        }
        var phaseIconHUD = phaseIconObj.GetComponent<PhaseIconHUD>() ?? phaseIconObj.AddComponent<PhaseIconHUD>();
        WireSerialized(phaseIconHUD, "m_PhaseIcon", iconImage);
        if (hudComp != null) WireSerialized(hudComp, "m_PhaseIconHUD", phaseIconHUD);

        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        Debug.Log("Slice2 scene setup complete: DayNightRenderWorker / NightVisionRoot tree / PhaseIconHUD wired");
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

    [MenuItem("WES/Tools/Setup DayNightWorker Scene")]
    public static void SetupDayNightWorkerScene()
    {
        var config = AssetDatabase.LoadAssetAtPath<DayNightConfig>(CONFIG_PATH);
        if (config == null)
        {
            Debug.LogError("DayNightConfig asset not found at " + CONFIG_PATH + ". Run 'Create DayNightConfig Asset' first.");
            return;
        }

        var existing = GameObject.Find("DayNightWorker");
        if (existing == null)
        {
            existing = new GameObject("DayNightWorker");
        }
        else
        {
            existing.transform.SetParent(null);
        }

        if (existing.GetComponent<NetworkObject>() == null)
            existing.AddComponent<NetworkObject>();
        var worker = existing.GetComponent<DayNightWorker>();
        if (worker == null)
            worker = existing.AddComponent<DayNightWorker>();

        var workerSO = new SerializedObject(worker);
        var configProp = workerSO.FindProperty("m_Config");
        if (configProp != null)
        {
            configProp.objectReferenceValue = config;
            workerSO.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogError("DayNightWorker.m_Config field not found");
        }

        var controller = Object.FindFirstObjectByType<InGameController>();
        if (controller == null)
        {
            Debug.LogError("InGameController not found in scene. Open Ingame scene first.");
            return;
        }
        var controllerSO = new SerializedObject(controller);
        var workerProp = controllerSO.FindProperty("m_DayNightWorker");
        if (workerProp != null)
        {
            workerProp.objectReferenceValue = worker;
            controllerSO.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogError("InGameController.m_DayNightWorker field not found");
        }

        EditorSceneManager.MarkSceneDirty(existing.scene);
        EditorSceneManager.SaveScene(existing.scene);
        Selection.activeGameObject = existing;
        Debug.Log("Setup complete: DayNightWorker GameObject created, NetworkObject + DayNightWorker components added, m_Config and m_DayNightWorker wired.");
    }

    [MenuItem("WES/Tools/Create DayNightConfig Asset")]
    public static void CreateDayNightConfig()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameResource"))
            AssetDatabase.CreateFolder("Assets", "GameResource");
        if (!AssetDatabase.IsValidFolder(CONFIG_FOLDER))
            AssetDatabase.CreateFolder("Assets/GameResource", "Config");

        var existing = AssetDatabase.LoadAssetAtPath<DayNightConfig>(CONFIG_PATH);
        if (existing != null)
        {
            Debug.Log("DayNightConfig already exists at " + CONFIG_PATH);
            Selection.activeObject = existing;
            return;
        }

        var asset = ScriptableObject.CreateInstance<DayNightConfig>();
        AssetDatabase.CreateAsset(asset, CONFIG_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = asset;
        Debug.Log("Created DayNightConfig asset at " + CONFIG_PATH);
    }
}
