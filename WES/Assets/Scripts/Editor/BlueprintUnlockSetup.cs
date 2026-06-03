using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 도면 해금(Blueprint Unlock) 리소스 셋업 에디터 도구.
/// - 도면 아이콘 sprite ×3 (양피지 톤 procedural)
/// - 잠금 오버레이 sprite (자물쇠/반투명 procedural)
/// - (선택) 해금 강조 프레임 sprite
/// 생성 후 ItemIcon/Image 폴더에 저장 + Addressable 등록.
/// 톤: 어둡고 외로운 야생 생존(Don't Starve). 바랜 양피지·잉크 라인. 화사한 골드/네온 금지.
/// </summary>
public static class BlueprintUnlockSetup
{
    private const int ICON_SIZE = 128;
    private const string ICON_DIR = "Assets/GameResource/Image/ItemIcon";
    private const string UI_IMAGE_DIR = "Assets/GameResource/Image/UI";

    // 바랜 양피지 팔레트 (어둡고 건조한 톤)
    private static readonly Color PARCHMENT_BG = new Color(0.42f, 0.36f, 0.27f, 1f);   // 바랜 양피지
    private static readonly Color PARCHMENT_EDGE = new Color(0.28f, 0.23f, 0.16f, 1f); // 찢긴 가장자리 그림자
    private static readonly Color INK = new Color(0.14f, 0.11f, 0.08f, 1f);            // 어두운 잉크 라인
    private static readonly Color INK_FAINT = new Color(0.22f, 0.18f, 0.13f, 1f);      // 흐린 보조선

    [MenuItem("WES/Tools/Setup Blueprint Resources")]
    public static void SetupBlueprintResources()
    {
        EnsureDir(ICON_DIR);
        EnsureDir(UI_IMAGE_DIR);

        // 1) 도면 아이콘 3종
        CreateBlueprintIcon("blueprint_shield_icon", DrawShield);
        CreateBlueprintIcon("blueprint_ironsword_icon", DrawSword);
        CreateBlueprintIcon("blueprint_leatherarmor_icon", DrawArmor);

        // 2) 잠금 오버레이 (반투명 검정 + 자물쇠)
        CreateLockOverlay("lock_overlay");

        // 3) 해금 강조 프레임 (양피지색 테두리, 가운데 투명)
        CreateHighlightFrame("blueprint_highlight_frame");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Addressable 등록
        RegisterAddressable($"{ICON_DIR}/blueprint_shield_icon.png", "blueprint_shield_icon");
        RegisterAddressable($"{ICON_DIR}/blueprint_ironsword_icon.png", "blueprint_ironsword_icon");
        RegisterAddressable($"{ICON_DIR}/blueprint_leatherarmor_icon.png", "blueprint_leatherarmor_icon");
        RegisterAddressable($"{UI_IMAGE_DIR}/lock_overlay.png", "lock_overlay");
        RegisterAddressable($"{UI_IMAGE_DIR}/blueprint_highlight_frame.png", "blueprint_highlight_frame");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BlueprintUnlockSetup] Blueprint sprites created + Addressable registered.");
    }

    // ---------------------------------------------------------------- 아이콘 생성

    private static void CreateBlueprintIcon(string _name, System.Action<Color[]> _drawSymbol)
    {
        var px = new Color[ICON_SIZE * ICON_SIZE];

        // 양피지 배경 + 미세한 얼룩(noise) + 찢긴 테두리
        for (int y = 0; y < ICON_SIZE; y++)
        {
            for (int x = 0; x < ICON_SIZE; x++)
            {
                int i = y * ICON_SIZE + x;
                float n = (Mathf.PerlinNoise(x * 0.08f, y * 0.08f) - 0.5f) * 0.10f;
                Color c = PARCHMENT_BG + new Color(n, n, n * 0.8f, 0f);

                // 둥근 잘린 모서리 (양피지 찢김)
                float edge = EdgeFactor(x, y);
                if (edge <= 0f) { px[i] = Color.clear; continue; }
                if (edge < 1f) c = Color.Lerp(PARCHMENT_EDGE, c, edge);
                px[i] = c;
            }
        }

        // 흐린 격자선(설계도 느낌) — 보조선
        for (int y = 0; y < ICON_SIZE; y++)
            for (int x = 0; x < ICON_SIZE; x++)
            {
                if (px[y * ICON_SIZE + x].a < 0.5f) continue;
                if ((x % 18 == 0 || y % 18 == 0))
                    px[y * ICON_SIZE + x] = Color.Lerp(px[y * ICON_SIZE + x], INK_FAINT, 0.35f);
            }

        // 중앙 심볼 잉크 라인
        _drawSymbol(px);

        SavePng(px, $"{ICON_DIR}/{_name}.png");
    }

    // 모서리 둥글림/찢김 계수 (0=투명, 1=불투명)
    private static float EdgeFactor(int x, int y)
    {
        float m = 6f; // 여백
        float fx = Mathf.Min(x - m, (ICON_SIZE - 1 - x) - m);
        float fy = Mathf.Min(y - m, (ICON_SIZE - 1 - y) - m);
        float f = Mathf.Min(fx, fy);
        if (f < 0f) return 0f;
        return Mathf.Clamp01(f / 4f);
    }

    private static void DrawShield(Color[] px)
    {
        // 방패 외곽 (위 넓고 아래 뾰족). row 0 = 화면 하단이므로 위=큰 y.
        int cx = ICON_SIZE / 2;
        for (int y = 24; y < 104; y++)
        {
            float t = (104f - y) / 80f;           // 0(아래)~1(위)
            float halfW = Mathf.Lerp(4f, 34f, Mathf.SmoothStep(0f, 1f, t));
            int xl = Mathf.RoundToInt(cx - halfW);
            int xr = Mathf.RoundToInt(cx + halfW);
            StrokeV(px, xl, y, INK); StrokeV(px, xr, y, INK);
        }
        // 상단(넓은 쪽) 가로선 + 중앙 보조선
        HLine(px, cx - 34, cx + 34, 103, INK);
        HLine(px, cx - 22, cx + 22, 64, INK_FAINT);
    }

    private static void DrawSword(Color[] px)
    {
        // 칼날: 칼끝 우상단, 손잡이 좌하단 (대각선). row 0 = 화면 하단.
        for (int t = 0; t <= 70; t++)
        {
            int x = 40 + t; int y = 36 + t;   // 좌하단(40,36) → 우상단(110,106)
            Plot(px, x, y, INK); Plot(px, x + 1, y, INK); Plot(px, x, y + 1, INK);
        }
        // 가드 (칼날과 손잡이 사이, 대각선에 수직)
        for (int t = -10; t <= 10; t++) Plot(px, 40 - t, 36 + t, INK);
        // 손잡이 (가드 아래로 짧게)
        for (int t = 0; t < 12; t++) { Plot(px, 36 - t, 32 - t, INK); Plot(px, 37 - t, 32 - t, INK); }
    }

    private static void DrawArmor(Color[] px)
    {
        int cx = ICON_SIZE / 2;
        // 흉갑 외곽 (어깨 넓고 허리 좁은 사다리꼴). row 0 = 화면 하단 → 어깨=큰 y.
        for (int y = 30; y < 100; y++)
        {
            float t = (100f - y) / 70f;          // 0(밑단)~1(어깨)
            float halfW = Mathf.Lerp(26f, 38f, t);
            int xl = Mathf.RoundToInt(cx - halfW);
            int xr = Mathf.RoundToInt(cx + halfW);
            StrokeV(px, xl, y, INK); StrokeV(px, xr, y, INK);
        }
        HLine(px, cx - 38, cx + 38, 99, INK);    // 어깨선 (상단)
        HLine(px, cx - 26, cx + 26, 31, INK);    // 밑단 (하단)
        // 목 V홈 (어깨 중앙에서 아래로)
        for (int t = 0; t < 16; t++) { Plot(px, cx - t, 99 - t, INK); Plot(px, cx + t, 99 - t, INK); }
        // 가운데 봉제선
        for (int y = 40; y < 86; y += 2) Plot(px, cx, y, INK_FAINT);
    }

    // ---------------------------------------------------------------- 잠금 오버레이

    private static void CreateLockOverlay(string _name)
    {
        var px = new Color[ICON_SIZE * ICON_SIZE];
        Color veil = new Color(0.05f, 0.05f, 0.06f, 0.62f); // 반투명 어두운 베일
        Color lockBody = new Color(0.62f, 0.60f, 0.55f, 0.95f); // 무채색 자물쇠
        Color lockEdge = new Color(0.30f, 0.29f, 0.26f, 0.95f);

        for (int i = 0; i < px.Length; i++) px[i] = veil;

        int cx = ICON_SIZE / 2;
        int bodyBottom = 44;   // 몸체 하단 (화면 아래)
        int bodyTop = 78;      // 몸체 상단 (화면 위) — 그 위에 고리

        // 자물쇠 몸체 (라운드 사각)
        for (int y = bodyBottom; y <= bodyTop; y++)
            for (int x = cx - 18; x <= cx + 18; x++)
            {
                bool edge = (x <= cx - 17 || x >= cx + 17 || y <= bodyBottom + 1 || y >= bodyTop - 1);
                Plot(px, x, y, edge ? lockEdge : lockBody);
            }
        // 열쇠구멍 (몸체 중앙)
        int khY = (bodyBottom + bodyTop) / 2 + 2;
        for (int y = bodyBottom + 8; y < khY + 6; y++)
            for (int x = cx - 3; x <= cx + 3; x++)
            {
                float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - khY) * (y - khY));
                if (r < 4f || (Mathf.Abs(x - cx) <= 1 && y < khY))
                    Plot(px, x, y, lockEdge);
            }
        // 고리(shackle) — 몸체 위 반원
        for (int a = 0; a <= 180; a += 3)
        {
            float rad = a * Mathf.Deg2Rad;
            int x = cx + Mathf.RoundToInt(Mathf.Cos(rad) * 12f);
            int y = bodyTop + Mathf.RoundToInt(Mathf.Sin(rad) * 13f);
            Plot(px, x, y, lockBody); Plot(px, x, y + 1, lockBody); Plot(px, x + 1, y, lockEdge);
        }

        SavePng(px, $"{UI_IMAGE_DIR}/{_name}.png");
    }

    // ---------------------------------------------------------------- 강조 프레임

    private static void CreateHighlightFrame(string _name)
    {
        var px = new Color[ICON_SIZE * ICON_SIZE];
        Color frame = new Color(0.55f, 0.47f, 0.33f, 1f);    // 바랜 양피지 테두리(건조)
        Color frameSoft = new Color(0.55f, 0.47f, 0.33f, 0.45f);

        int thick = 5;
        for (int y = 0; y < ICON_SIZE; y++)
            for (int x = 0; x < ICON_SIZE; x++)
            {
                int i = y * ICON_SIZE + x;
                int d = Mathf.Min(Mathf.Min(x, ICON_SIZE - 1 - x), Mathf.Min(y, ICON_SIZE - 1 - y));
                if (d < thick) px[i] = frame;
                else if (d < thick + 3) px[i] = frameSoft;
                else px[i] = Color.clear;
            }
        SavePng(px, $"{UI_IMAGE_DIR}/{_name}.png");
    }

    // ---------------------------------------------------------------- 픽셀 헬퍼

    private static void Plot(Color[] px, int x, int y, Color c)
    {
        if (x < 0 || x >= ICON_SIZE || y < 0 || y >= ICON_SIZE) return;
        int i = y * ICON_SIZE + x;
        // 알파 블렌드 (라인이 양피지 위에 얹히도록)
        Color bg = px[i];
        float a = c.a;
        px[i] = new Color(
            Mathf.Lerp(bg.r, c.r, a),
            Mathf.Lerp(bg.g, c.g, a),
            Mathf.Lerp(bg.b, c.b, a),
            Mathf.Max(bg.a, a));
    }

    private static void StrokeV(Color[] px, int x, int y, Color c)
    {
        Plot(px, x, y, c); Plot(px, x + 1, y, c);
    }

    private static void HLine(Color[] px, int x0, int x1, int y, Color c)
    {
        for (int x = x0; x <= x1; x++) { Plot(px, x, y, c); Plot(px, x, y + 1, c); }
    }

    // ---------------------------------------------------------------- 저장 / 임포트 / Addressable

    private static void SavePng(Color[] px, string path)
    {
        var tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        SetSpriteImport(path);
    }

    private static void SetSpriteImport(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
    }

    private static void RegisterAddressable(string assetPath, string address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) { Debug.LogError("[BlueprintUnlockSetup] Addressable settings not found."); return; }
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) { Debug.LogError($"[BlueprintUnlockSetup] GUID not found: {assetPath}"); return; }
        var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.address = address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    private static void EnsureDir(string dir)
    {
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    // ================================================================ S3+S4 와이어링

    private const string CRAFT_POPUP_PREFAB = "Assets/GameResource/UI/Popup/CraftPopup.prefab";
    private const string TOAST_PREFAB = "Assets/GameResource/UI/HUD/BlueprintToast.prefab";

    /// <summary>
    /// S3+S4 도면 해금 UI 와이어링.
    /// (a) CraftPopup.prefab 내 CraftScrollCell.m_LockOverlay 슬롯 연결
    /// (b) BlueprintToast.prefab 에 BlueprintToast 컴포넌트 부착 + m_CanvasGroup/m_MessageText 슬롯 연결
    /// (c) Ingame 씬의 InGameHUDWorker 하위에 BlueprintToast 인스턴스 배치 + m_BlueprintToast 슬롯 연결
    /// </summary>
    [MenuItem("WES/Tools/Wire Blueprint Unlock UI")]
    public static void WireBlueprintUnlockUI()
    {
        WireCraftScrollCellLockOverlay();
        WireBlueprintToastPrefab();
        WireHudWorkerToast();
        Debug.Log("[BlueprintUnlockSetup] Blueprint Unlock UI wiring complete (CraftScrollCell / BlueprintToast / InGameHUDWorker).");
    }

    // (a) CraftScrollCell.m_LockOverlay <- 셀 하위 "LockOverlay" GameObject
    private static void WireCraftScrollCellLockOverlay()
    {
        var root = PrefabUtility.LoadPrefabContents(CRAFT_POPUP_PREFAB);
        if (root == null) { Debug.LogError($"[BlueprintUnlockSetup] CraftPopup prefab not found: {CRAFT_POPUP_PREFAB}"); return; }

        try
        {
            var cell = root.GetComponentInChildren<CraftScrollCell>(true);
            if (cell == null) { Debug.LogError("[BlueprintUnlockSetup] CraftScrollCell not found in CraftPopup prefab."); return; }

            var lockOverlay = FindChildByName(cell.transform, "LockOverlay");
            if (lockOverlay == null) { Debug.LogError("[BlueprintUnlockSetup] 'LockOverlay' GameObject not found under CraftScrollCell."); return; }

            lockOverlay.SetActive(false); // 기본 비활성
            WireSerialized(cell, "m_LockOverlay", lockOverlay);

            PrefabUtility.SaveAsPrefabAsset(root, CRAFT_POPUP_PREFAB);
            Debug.Log("[BlueprintUnlockSetup] CraftScrollCell.m_LockOverlay wired.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // (b) BlueprintToast 컴포넌트 부착 + 슬롯 연결
    private static void WireBlueprintToastPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(TOAST_PREFAB);
        if (root == null) { Debug.LogError($"[BlueprintUnlockSetup] BlueprintToast prefab not found: {TOAST_PREFAB}"); return; }

        try
        {
            var toast = root.GetComponent<BlueprintToast>();
            if (toast == null)
                toast = root.AddComponent<BlueprintToast>();

            var canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = root.AddComponent<CanvasGroup>();

            var messageGo = FindChildByName(root.transform, "MessageText");
            TextMeshProUGUI messageText = messageGo != null ? messageGo.GetComponent<TextMeshProUGUI>() : null;
            if (messageText == null) { Debug.LogError("[BlueprintUnlockSetup] 'MessageText' (TMP) not found under BlueprintToast."); return; }

            WireSerialized(toast, "m_CanvasGroup", canvasGroup);
            WireSerialized(toast, "m_MessageText", messageText);

            PrefabUtility.SaveAsPrefabAsset(root, TOAST_PREFAB);
            Debug.Log("[BlueprintUnlockSetup] BlueprintToast component attached + m_CanvasGroup/m_MessageText wired.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // (c) Ingame 씬: InGameHUDWorker 하위에 BlueprintToast 인스턴스 배치 + 슬롯 연결
    private static void WireHudWorkerToast()
    {
        var hudWorker = Object.FindFirstObjectByType<InGameHUDWorker>(FindObjectsInactive.Include);
        if (hudWorker == null)
        {
            Debug.LogError("[BlueprintUnlockSetup] InGameHUDWorker not found in open scene. Open Ingame.unity first.");
            return;
        }

        var toastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TOAST_PREFAB);
        if (toastPrefab == null) { Debug.LogError($"[BlueprintUnlockSetup] BlueprintToast prefab not found: {TOAST_PREFAB}"); return; }

        // 기존 인스턴스 재사용(중복 배치 방지)
        var existing = hudWorker.GetComponentInChildren<BlueprintToast>(true);
        GameObject toastGo;
        if (existing != null)
        {
            toastGo = existing.gameObject;
        }
        else
        {
            toastGo = (GameObject)PrefabUtility.InstantiatePrefab(toastPrefab, hudWorker.transform);
            toastGo.name = "BlueprintToast";
            var rt = toastGo.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -346f);
            }
        }

        var toast = toastGo.GetComponent<BlueprintToast>();
        WireSerialized(hudWorker, "m_BlueprintToast", toast);

        EditorSceneManager.MarkSceneDirty(hudWorker.gameObject.scene);
        EditorSceneManager.SaveScene(hudWorker.gameObject.scene);
        Debug.Log("[BlueprintUnlockSetup] InGameHUDWorker.m_BlueprintToast wired (BlueprintToast placed under HUD).");
    }

    private static GameObject FindChildByName(Transform _root, string _name)
    {
        if (_root.name == _name)
            return _root.gameObject;

        var all = _root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t.name == _name)
                return t.gameObject;
        }
        return null;
    }

    private static void WireSerialized(Object _target, string _propName, Object _value)
    {
        if (_target == null) { Debug.LogError($"[BlueprintUnlockSetup] WireSerialized: target null for {_propName}"); return; }
        var so = new SerializedObject(_target);
        var prop = so.FindProperty(_propName);
        if (prop == null) { Debug.LogError($"[BlueprintUnlockSetup] Property '{_propName}' not found on {_target.GetType().Name}"); return; }
        prop.objectReferenceValue = _value;
        so.ApplyModifiedProperties();
    }
}
