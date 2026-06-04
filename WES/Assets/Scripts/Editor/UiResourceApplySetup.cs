using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Phase3 UI 리소스 프리팹 적용 일회성 Setup (designer-b).
/// 적용명세(document/design/client-spec/ui-resource-apply/적용명세.md) 2~5절 수행:
/// - 게이지 스킨(PlayerStatusHUD): 프레임 배경 Sliced + fill sprite Simple(옵션A, 코드무변경)
/// - 버튼 3-state SpriteSwap(LoginPopup/LobbyPopup/LobbyRoomPopup)
/// - 패널/슬롯 9-slice(팝업 배경·QuickSlot·Inventory)
/// - 배경·로고(LoginPopup/LobbyPopup/ResultPopup) Simple
/// MCP set_property가 Vector2/Sprite/Image.type 일괄 처리에 약해 코드로 정확히 박는다.
/// 프리팹 에셋을 직접 로드·수정·저장(designer-b 단독 프리팹 편집 — client 코드와 충돌 0).
/// </summary>
public static class UiResourceApplySetup
{
    const string FRAME = "Assets/GameResource/UI/Frame";
    const string GAUGE = "Assets/GameResource/UI/Gauge";
    const string BG = "Assets/GameResource/UI/Background";
    const string POPUP = "Assets/GameResource/UI/Popup";
    const string HUD = "Assets/GameResource/UI/HUD";

    // ── QA B3/LOW: InventoryPopup 배경 비대칭 수정 ───────────────
    // 원인: InventoryWindow의 panel_frame이 color(0.15,0.15,0.18)로 어둡게 곱해져 나무 질감이
    // 묻히고, DetailPanel(우반부)이 불투명 단색 갈색으로 덮어 좌(투명 ContentArea→panel 비침)/
    // 우(단색) 비대칭. → 창 배경 color를 밝게(질감 노출) + DetailPanel에도 panel_frame 입혀 통일.
    [MenuItem("WES/AI Texture/Fix InventoryPopup BG Symmetry (QA B3)")]
    static void FixInventoryBgSymmetry()
    {
        string path = $"{POPUP}/InventoryPopup.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var panel = LoadSprite(FRAME, "panel_frame");
            // 창 전체 배경: panel_frame 질감이 제대로 보이도록 color를 중립 밝기로(원본 흙빛 톤 노출)
            var win = root.transform.Find("InventoryWindow");
            if (win != null)
            {
                var img = win.GetComponent<Image>();
                if (img != null) { img.sprite = panel; img.type = Image.Type.Sliced; img.color = Color.white; }
            }
            // DetailPanel: 단색 → panel_frame Sliced로 통일(좌우 동일 나무 질감), 약간 어둡게 곱해 패널 구분
            var detail = root.transform.Find("InventoryWindow/ContentArea/DetailPanel");
            if (detail != null)
            {
                var img = detail.GetComponent<Image>();
                if (img != null) { img.sprite = panel; img.type = Image.Type.Sliced; img.color = new Color(0.8f, 0.78f, 0.74f, 1f); }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UiApply] InventoryPopup 배경 대칭 수정(QA B3) 완료.");
    }

    // ── QA B3 오판정 철회 → 원복 ─────────────────────────────────
    // QA가 B3(인벤 배경 비대칭)를 오판정으로 철회(우측 패널은 정상 DetailPanel 2단 레이아웃).
    // FixInventoryBgSymmetry의 변경을 원래 디자인 값으로 되돌린다.
    // 원래: InventoryWindow=panel_frame Sliced + color(0.15,0.15,0.18,1) 어두운 청회,
    //       DetailPanel=sprite None(단색) + color(0.18,0.16,0.13,1) 갈색.
    [MenuItem("WES/AI Texture/Revert InventoryPopup BG (QA B3 철회)")]
    static void RevertInventoryBg()
    {
        string path = $"{POPUP}/InventoryPopup.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var win = root.transform.Find("InventoryWindow");
            if (win != null)
            {
                var img = win.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = LoadSprite(FRAME, "panel_frame"); // panel_frame은 유지(원래도 있었음)
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.15f, 0.15f, 0.18f, 1f); // 원래 어두운 청회 톤 복구
                }
            }
            var detail = root.transform.Find("InventoryWindow/ContentArea/DetailPanel");
            if (detail != null)
            {
                var img = detail.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = null;                 // 원래 단색(sprite None)
                    img.type = Image.Type.Simple;
                    img.color = new Color(0.18f, 0.16f, 0.13f, 1f); // 원래 갈색
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UiApply] InventoryPopup 배경 원복(QA B3 철회) 완료.");
    }

    [MenuItem("WES/AI Texture/Apply UI Resources To Prefabs")]
    public static void ApplyAll()
    {
        ApplyPlayerStatusHUD();
        ApplyQuickSlotHUD();
        ApplyLoginPopup();
        ApplyLobbyPopup();
        ApplyLobbyRoomPopup();
        ApplyInventoryPopup();
        ApplyCraftPopup();
        ApplyResultPopup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UiApply] 전체 프리팹 적용 완료.");
    }

    // ── 2절 게이지 ────────────────────────────────────────────────
    static void ApplyPlayerStatusHUD()
    {
        string path = $"{HUD}/PlayerStatusHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var frame = LoadSprite(GAUGE, "gauge_frame_empty");
            var hpFill = LoadSprite(GAUGE, "gauge_fill_hp");
            var coldFill = LoadSprite(GAUGE, "gauge_fill_cold");

            // HealthStatus/ColdStatus 루트 Image = 프레임 배경(Sliced)
            SetImage(root, "HealthStatus", frame, Image.Type.Sliced);
            SetImage(root, "ColdStatus", frame, Image.Type.Sliced);
            // Gauge/Fill = fill sprite(Simple, anchorMax 방식 유지 — 옵션A)
            SetImage(root, "HealthStatus/Gauge/Fill", hpFill, Image.Type.Simple);
            SetImage(root, "ColdStatus/Gauge/Fill", coldFill, Image.Type.Simple);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 4절 QuickSlot 슬롯 ───────────────────────────────────────
    static void ApplyQuickSlotHUD()
    {
        string path = $"{HUD}/QuickSlotHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var slot = LoadSprite(FRAME, "slot_frame");
            for (int i = 0; i < 8; i++)
                SetImage(root, $"QuickSlotCell_{i}", slot, Image.Type.Sliced);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 3절 버튼 + 1절(I-1/I-2) 배경·로고: LoginPopup ─────────────
    static void ApplyLoginPopup()
    {
        string path = $"{POPUP}/LoginPopup.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 배경
            SetImage(root, "BG", LoadSprite(BG, "bg_title"), Image.Type.Simple);
            // 로고 신규 Image (BG 다음, 버튼 앞)
            EnsureLogoImage(root.transform, LoadSprite(BG, "logo_main"));
            // 버튼 3-state
            ApplyButton(root, "StartButton");
            ApplyButton(root, "OptionButton");
            ApplyButton(root, "ExitButton");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ApplyLobbyPopup()
    {
        string path = $"{POPUP}/LobbyPopup.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SetImage(root, "BG", LoadSprite(BG, "bg_lobby"), Image.Type.Simple);
            ApplyButton(root, "RoomCreateButton");
            ApplyButton(root, "RoomEnterButton");
            ApplyButton(root, "EnterCode/EnterButton");
            // 입력필드 배경 패널 9-slice
            SetImage(root, "EnterCode/BG", LoadSprite(FRAME, "panel_frame"), Image.Type.Sliced);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ApplyLobbyRoomPopup()
    {
        string path = $"{POPUP}/LobbyRoomPopup.prefab";
        if (!File.Exists(path)) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 버튼류 전수 3-state (이름 모를 수 있어 Button 가진 노드 전부 적용)
            ApplyAllButtons(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 4절 패널/슬롯: Inventory / Craft (옵션A — FrameBg 별도 노드) ──
    // 셀 코드(InventoryScrollCell/CraftScrollCell)가 m_BackgroundImage.color를 런타임에
    // 덮어쓰므로(빈/채움/선택 상태색), slot_frame을 셀 루트 배경에 직접 넣으면 색이 곱해진다.
    // → 셀 루트 배경은 건드리지 않고(color 마스킹 유지), 별도 FrameBg Image 자식을 추가해
    //   칸 테두리(slot_frame, Sliced)를 입힌다. FrameBg는 셀 색변경 대상이 아니라 톤 안정.
    //   CellTemplate은 BaseScroll이 풀링 복제하므로 템플릿에만 넣으면 전 셀 적용.
    [MenuItem("WES/AI Texture/Apply Inventory+Craft Slots (FrameBg node)")]
    static void ApplyInventoryAndCraftSlots()
    {
        ApplyInventoryPopup();
        ApplyCraftPopup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UiApply] Inventory/Craft 슬롯셀 FrameBg(slot_frame) 적용 완료.");
    }

    static void ApplyInventoryPopup()
    {
        string path = $"{POPUP}/InventoryPopup.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 창 패널 9-slice
            SetImageIfExists(root, "InventoryWindow", LoadSprite(FRAME, "panel_frame"), Image.Type.Sliced);
            // 셀 루트 배경 sprite는 None으로 복구(color 마스킹 유지) + FrameBg 자식 추가
            var cell = root.transform.Find(
                "InventoryWindow/ContentArea/InventoryScroll/Viewport/Content/CellTemplate");
            if (cell != null)
            {
                ResetCellRootImage(cell);
                EnsureFrameBg(cell);
            }
            else Debug.LogWarning("[UiApply] InventoryPopup CellTemplate 없음.");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ApplyCraftPopup()
    {
        string path = $"{POPUP}/CraftPopup.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SetImageIfExists(root, "PopupPanel", LoadSprite(FRAME, "panel_frame"), Image.Type.Sliced);
            // 레시피 카드 셀(CellTemplate/CraftScrollCell에 Image+CraftScrollCell)
            var cell = root.transform.Find(
                "PopupPanel/LeftPanel/CraftScroll/Viewport/Content/CellTemplate/CraftScrollCell");
            if (cell != null)
            {
                ResetCellRootImage(cell);
                EnsureFrameBg(cell);
            }
            else Debug.LogWarning("[UiApply] CraftPopup CraftScrollCell 없음.");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>셀 루트 Image sprite를 None으로 되돌린다(셀 코드 color 마스킹 유지용 단색 배경).</summary>
    static void ResetCellRootImage(Transform cell)
    {
        var img = cell.GetComponent<Image>();
        if (img == null) return;
        img.sprite = null;
        img.type = Image.Type.Simple;
    }

    /// <summary>셀에 칸 테두리 FrameBg(slot_frame, Sliced) 자식 추가. 맨 뒤(sibling 0) + 약간 바깥 여백.</summary>
    static void EnsureFrameBg(Transform cell)
    {
        var existing = cell.Find("FrameBg");
        GameObject go = existing != null ? existing.gameObject
            : new GameObject("FrameBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) go.transform.SetParent(cell, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        // 약간 바깥 여백(칸 테두리처럼) — 좌우상하 -4px 확장
        rt.offsetMin = new Vector2(-4f, -4f);
        rt.offsetMax = new Vector2(4f, 4f);
        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite(FRAME, "slot_frame");
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        go.transform.SetSiblingIndex(0); // 맨 뒤로 렌더(아이콘·텍스트가 위에)
    }

    // ── 5절 ResultPopup 성공/전멸 배경 + ConfirmButton 3-state ───
    [MenuItem("WES/AI Texture/Apply ResultPopup Only (bg+confirm btn)")]
    static void ApplyResultPopup()
    {
        string path = $"{POPUP}/ResultPopup.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // BackgroundPanel 단일 → 그 하위에 BgSuccess/BgDefeat Image 2개 생성(텍스트보다 뒤로)
            var panel = root.transform.Find("BackgroundPanel");
            if (panel != null)
            {
                var success = EnsureChildImage(panel, "BgSuccess", LoadSprite(BG, "bg_clear_success"));
                var defeat = EnsureChildImage(panel, "BgDefeat", LoadSprite(BG, "bg_clear_defeat"));
                // 맨 뒤(자식 인덱스 0,1)로 보내 텍스트/버튼이 위에 오게
                success.transform.SetSiblingIndex(0);
                defeat.transform.SetSiblingIndex(1);
                // 기본 둘 다 비활성 — client 토글 코드가 켠다
                success.SetActive(false);
                defeat.SetActive(false);
            }
            // ConfirmButton 3-state SpriteSwap (통일성 — team-lead 지시)
            ApplyButton(root, "BackgroundPanel/ConfirmButton");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────

    static void ApplyButton(GameObject root, string relPath)
    {
        var t = root.transform.Find(relPath);
        if (t == null) { Debug.LogWarning($"[UiApply] 버튼 없음: {relPath}"); return; }
        ApplyButtonState(t.gameObject);
    }

    static void ApplyAllButtons(Transform node)
    {
        var btn = node.GetComponent<Button>();
        if (btn != null) ApplyButtonState(node.gameObject);
        for (int i = 0; i < node.childCount; i++)
            ApplyAllButtons(node.GetChild(i));
    }

    static void ApplyButtonState(GameObject go)
    {
        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        if (img == null || btn == null) return;

        var idle = LoadSprite(FRAME, "btn_frame_idle");
        var hover = LoadSprite(FRAME, "btn_frame_hover");
        var disabled = LoadSprite(FRAME, "btn_frame_disabled");

        img.sprite = idle;
        img.type = Image.Type.Sliced;

        btn.transition = Selectable.Transition.SpriteSwap;
        var ss = btn.spriteState;
        ss.highlightedSprite = hover;
        ss.pressedSprite = hover;
        ss.selectedSprite = hover;
        ss.disabledSprite = disabled;
        btn.spriteState = ss;
        // targetGraphic 보장
        if (btn.targetGraphic == null) btn.targetGraphic = img;
    }

    static void SetImage(GameObject root, string relPath, Sprite sprite, Image.Type type)
    {
        var t = root.transform.Find(relPath);
        if (t == null) { Debug.LogWarning($"[UiApply] 노드 없음: {relPath}"); return; }
        var img = t.GetComponent<Image>();
        if (img == null) { Debug.LogWarning($"[UiApply] Image 없음: {relPath}"); return; }
        img.sprite = sprite;
        img.type = type;
    }

    static void SetImageIfExists(GameObject root, string relPath, Sprite sprite, Image.Type type)
    {
        var t = root.transform.Find(relPath);
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img == null) return;
        img.sprite = sprite;
        img.type = type;
    }

    static void EnsureLogoImage(Transform parent, Sprite logo)
    {
        var existing = parent.Find("Logo");
        GameObject go = existing != null ? existing.gameObject
            : new GameObject("Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        // 상단 중앙 배치(타이틀 위치)
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -120f);
        rt.sizeDelta = new Vector2(900f, 300f);
        var img = go.GetComponent<Image>();
        img.sprite = logo;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = false;
        go.transform.SetSiblingIndex(1); // BG(0) 다음
    }

    static GameObject EnsureChildImage(Transform parent, string name, Sprite sprite)
    {
        var existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return go;
    }

    static Sprite LoadSprite(string dir, string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.png");
        if (s == null) Debug.LogWarning($"[UiApply] sprite 없음: {dir}/{name}.png");
        return s;
    }
}
