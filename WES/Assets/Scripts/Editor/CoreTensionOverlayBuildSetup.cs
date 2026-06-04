using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 코어텐션 오버레이 GameObject 생성·와이어링 일회성 Setup (designer-b 프리팹/씬 작업).
/// MCP set_property가 RectTransform Vector2(anchor) 세팅을 지원하지 않아 코드로 처리.
/// Ingame 씬의 Canvas/InGameHUDWorker/CoreTensionOverlay 하위에 7개 풀스크린 오버레이를
/// 생성하고, CoreTensionOverlayWorker의 SerializeField 슬롯에 연결한 뒤
/// InGameHUDWorker.m_CoreTensionOverlay 에도 연결한다. (적용명세 1절)
///
/// 멱등: 기존 오버레이 자식을 지우고 재생성. 와이어링은 SerializedObject로 박는다.
/// </summary>
public static class CoreTensionOverlayBuildSetup
{
    private const string SRC = "Assets/GameResource/UI/CoreTension";
    private const string OVERLAY_PATH = "Canvas/InGameHUDWorker/CoreTensionOverlay";
    private const string HUD_PATH = "Canvas/InGameHUDWorker";

    /// <summary>
    /// DeathOverlay GO를 루트 Canvas 직속(Canvas/DeathOverlay, 맨 위 sibling)으로 이동한다.
    /// HUD(InGameHUDWorker) 비활성에도 안 꺼져 GameOver 암전이 ResultPopup 위로 유지되게 함.
    /// (director b 결정 / client 명세) 슬롯 참조(m_DeathOverlay)는 다른 부모여도 유효 → 재연결 보장.
    /// 나머지 6오버레이는 CoreTensionOverlay 하위 유지.
    /// </summary>
    [MenuItem("WES/AI Texture/Move DeathOverlay To Root Canvas")]
    public static void MoveDeathOverlayToRootCanvas()
    {
        var overlayGo = GameObject.Find(OVERLAY_PATH);
        if (overlayGo == null) { Debug.LogError($"[CoreTension] {OVERLAY_PATH} 없음."); return; }
        var worker = overlayGo.GetComponent<CoreTensionOverlayWorker>();

        var death = GameObject.Find(OVERLAY_PATH + "/DeathOverlay")
                    ?? GameObject.Find("Canvas/DeathOverlay"); // 이미 옮긴 경우 멱등
        if (death == null) { Debug.LogError("[CoreTension] DeathOverlay GO 없음."); return; }

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[CoreTension] 루트 Canvas 없음."); return; }

        // 루트 Canvas 직속으로 이동(스케일/RectTransform은 아래서 풀스트레치 재설정)
        death.transform.SetParent(canvas.transform, worldPositionStays: false);
        death.transform.SetAsLastSibling(); // 맨 위 렌더(다른 HUD/팝업보다 앞)
        StretchFull(death.GetComponent<RectTransform>());

        var img = death.GetComponent<Image>();
        if (img != null) { img.raycastTarget = false; var c = img.color; c.a = 0f; img.color = c; }

        // 슬롯 재연결(부모 바뀌어도 참조 유지 보장)
        if (worker != null)
        {
            var so = new SerializedObject(worker);
            so.FindProperty("m_DeathOverlay").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(death.scene);
        EditorSceneManager.SaveScene(death.scene);
        Debug.Log("[CoreTension] DeathOverlay → Canvas 직속 이동 + m_DeathOverlay 재연결 + 씬 저장 완료.");
    }

    [MenuItem("WES/AI Texture/Apply CoreTension Overlay (scene wiring)")]
    public static void Build()
    {
        var overlayGo = GameObject.Find(OVERLAY_PATH);
        if (overlayGo == null)
        {
            Debug.LogError($"[CoreTensionBuild] {OVERLAY_PATH} 없음. Ingame 씬을 열고 CoreTensionOverlay GameObject를 InGameHUDWorker 하위에 먼저 생성하라.");
            return;
        }

        var worker = overlayGo.GetComponent<CoreTensionOverlayWorker>();
        if (worker == null)
        {
            Debug.LogError("[CoreTensionBuild] CoreTensionOverlayWorker 컴포넌트 없음.");
            return;
        }

        // 컨테이너 풀스크린 stretch
        StretchFull(overlayGo.GetComponent<RectTransform>());

        // 기존 자식 정리(멱등)
        for (int i = overlayGo.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(overlayGo.transform.GetChild(i).gameObject);

        // 렌더 순서(형제 순서): 아래→위. 명세 1-A 순서.
        var dayNight = CreateImage(overlayGo.transform, "DayNightTint", "daynight_tint_white", 0.18f);
        var fog      = CreateRawImage(overlayGo.transform, "AmbientFog", "ambient_fog");
        var cold1    = CreateImage(overlayGo.transform, "ColdOverlay1", "cold_overlay_1", 0f);
        var cold2    = CreateImage(overlayGo.transform, "ColdOverlay2", "cold_overlay_2", 0f);
        var cold3    = CreateImage(overlayGo.transform, "ColdOverlay3", "cold_overlay_3", 0f);
        var hpVig    = CreateImage(overlayGo.transform, "HpVignette", "vignette_red", 0f);
        var death    = CreateImage(overlayGo.transform, "DeathOverlay", "death_overlay", 0f);

        // Worker 슬롯 와이어링
        var so = new SerializedObject(worker);
        so.FindProperty("m_ColdOverlay1").objectReferenceValue = cold1;
        so.FindProperty("m_ColdOverlay2").objectReferenceValue = cold2;
        so.FindProperty("m_ColdOverlay3").objectReferenceValue = cold3;
        so.FindProperty("m_HpVignette").objectReferenceValue = hpVig;
        so.FindProperty("m_DeathOverlay").objectReferenceValue = death;
        so.FindProperty("m_DayNightTint").objectReferenceValue = dayNight;
        so.FindProperty("m_AmbientFog").objectReferenceValue = fog;
        so.ApplyModifiedPropertiesWithoutUndo();

        // InGameHUDWorker.m_CoreTensionOverlay 연결
        var hudGo = GameObject.Find(HUD_PATH);
        var hud = hudGo != null ? hudGo.GetComponent<InGameHUDWorker>() : null;
        if (hud != null)
        {
            var hso = new SerializedObject(hud);
            var prop = hso.FindProperty("m_CoreTensionOverlay");
            if (prop != null)
            {
                prop.objectReferenceValue = worker;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[CoreTensionBuild] InGameHUDWorker.m_CoreTensionOverlay 프로퍼티 없음.");
        }
        else Debug.LogWarning("[CoreTensionBuild] InGameHUDWorker 없음.");

        EditorSceneManager.MarkSceneDirty(overlayGo.scene);
        EditorSceneManager.SaveScene(overlayGo.scene);
        Debug.Log("[CoreTensionBuild] 오버레이 7종 생성·와이어링·씬 저장 완료.");
    }

    static Image CreateImage(Transform parent, string name, string spriteName, float alpha)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        StretchFull(go.GetComponent<RectTransform>());
        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite(spriteName);
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        var c = Color.white; c.a = alpha; img.color = c;
        return img;
    }

    static RawImage CreateRawImage(Transform parent, string name, string texName)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        StretchFull(go.GetComponent<RectTransform>());
        var raw = go.GetComponent<RawImage>();
        raw.texture = LoadTexture(texName);
        raw.raycastTarget = false;
        // texture 알파 그대로 노출. uvRect는 코드가 스크롤.
        return raw;
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
        if (s == null) Debug.LogWarning($"[CoreTensionBuild] sprite 없음: {SRC}/{name}.png");
        return s;
    }

    static Texture2D LoadTexture(string name)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SRC}/{name}.png");
        if (t == null) Debug.LogWarning($"[CoreTensionBuild] texture 없음: {SRC}/{name}.png");
        return t;
    }
}
