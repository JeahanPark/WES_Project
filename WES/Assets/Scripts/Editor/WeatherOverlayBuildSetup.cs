using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// R4 ① 날씨 오버레이 GameObject 생성·와이어링 일회성 Setup (designer 영역).
/// CoreTensionOverlayBuildSetup 패턴 계승. Ingame 씬의 연출 전용 캔버스 하위(sortingOrder -10,
/// HUD 아래)의 CoreTensionOverlay GameObject에 풀스크린 Image 3개(비/안개/눈보라)를 추가하고
/// CoreTensionOverlayWorker의 m_RainEdge/m_FogVeil/m_SnowstormOverlay 슬롯에 연결한다.
///
/// 기존 6오버레이(DayNightTint/AmbientFog/Cold1~3/HpVignette)는 건드리지 않고,
/// 날씨 3종만 멱등 추가(이미 있으면 갱신). 초기 알파 0(코드 ApplyWeatherOverlay가 제어).
///
/// MCP set_reference로도 슬롯 연결 가능하나, RectTransform anchor 풀스트레치 세팅을
/// 한 번에 처리하기 위해 코드로 일괄 처리(CoreTension과 동일 이유).
/// </summary>
public static class WeatherOverlayBuildSetup
{
    private const string SRC = "Assets/GameResource/UI/CoreTension";
    private const string OVERLAY_PATH = "Canvas_CoreTension/CoreTensionOverlay";

    // 렌더 순서: 비는 cold 위(가장자리 얼룩), 안개/눈보라는 풀스크린 차폐 — 코어텐션 위에 올린다.
    [MenuItem("WES/AI Texture/Apply Weather Overlay (scene wiring)")]
    public static void Build()
    {
        var overlayGo = GameObject.Find(OVERLAY_PATH);
        if (overlayGo == null)
        {
            Debug.LogError($"[WeatherBuild] {OVERLAY_PATH} 없음. Ingame 씬을 열고 CoreTensionOverlay를 확인하라.");
            return;
        }

        var worker = overlayGo.GetComponent<CoreTensionOverlayWorker>();
        if (worker == null)
        {
            Debug.LogError("[WeatherBuild] CoreTensionOverlayWorker 컴포넌트 없음.");
            return;
        }

        // 날씨 3종 Image 멱등 생성(기존 6오버레이는 보존). 형제 순서는 맨 뒤(코어텐션 위에 합성).
        var rain = UpsertImage(overlayGo.transform, "RainEdge", "rain_edge");
        var fog  = UpsertImage(overlayGo.transform, "FogVeil", "fog_veil");
        var snow = UpsertImage(overlayGo.transform, "SnowstormOverlay", "snowstorm_white");

        // Worker 슬롯 와이어링(set_reference 동등). 초기 알파 0은 UpsertImage가 보장.
        var so = new SerializedObject(worker);
        so.FindProperty("m_RainEdge").objectReferenceValue = rain;
        so.FindProperty("m_FogVeil").objectReferenceValue = fog;
        so.FindProperty("m_SnowstormOverlay").objectReferenceValue = snow;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(overlayGo.scene);
        EditorSceneManager.SaveScene(overlayGo.scene);
        Debug.Log("[WeatherBuild] 날씨 오버레이 3종 생성·와이어링·씬 저장 완료.");
    }

    /// <summary>이름으로 기존 자식을 찾으면 sprite/세팅 갱신, 없으면 생성. 초기 알파 0.</summary>
    static Image UpsertImage(Transform parent, string name, string spriteName)
    {
        var existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
            if (go.GetComponent<CanvasRenderer>() == null) go.AddComponent<CanvasRenderer>();
            if (go.GetComponent<Image>() == null) go.AddComponent<Image>();
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
        }

        StretchFull(go.GetComponent<RectTransform>());
        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite(spriteName);
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        var c = Color.white; c.a = 0f; img.color = c; // 초기 알파 0 — 코드가 제어
        return img;
    }

    static void StretchFull(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }

    static Sprite LoadSprite(string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{SRC}/{name}.png");
        if (s == null) Debug.LogWarning($"[WeatherBuild] sprite 없음: {SRC}/{name}.png (먼저 WES/AI Texture/Generate Weather Procedural Sources 실행)");
        return s;
    }
}
