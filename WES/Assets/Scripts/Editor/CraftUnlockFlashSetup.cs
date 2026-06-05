using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// 도면해금 테두리 반짝 프레임(G-9) 일회성 Setup.
/// CraftPopup 프리팹 내부 CraftScrollCell 템플릿에 테두리 Image 자식을 추가하고
/// CraftScrollCell.m_UnlockFlashFrame 슬롯에 연결한다.
/// 자산: 기존 blueprint_highlight_frame.png(빛바랜 양피지 테두리, 9-slice) 재사용 — 기획 §8.3 건조한 톤.
/// 멱등: 이미 UnlockFlashFrame 자식이 있으면 sprite/슬롯만 재연결.
/// </summary>
public static class CraftUnlockFlashSetup
{
    private const string PREFAB = "Assets/GameResource/UI/Popup/CraftPopup.prefab";
    private const string FRAME_SPRITE = "Assets/GameResource/Image/UI/blueprint_highlight_frame.png";
    private const string CELL_NAME = "CraftScrollCell";
    private const string FRAME_NAME = "UnlockFlashFrame";

    [MenuItem("WES/AI Texture/Wire CraftCell UnlockFlashFrame")]
    public static void Wire()
    {
        var root = PrefabUtility.LoadPrefabContents(PREFAB);
        try
        {
            var cell = FindDeep(root.transform, CELL_NAME);
            if (cell == null) { Debug.LogError($"[UnlockFlash] {CELL_NAME} 못 찾음."); return; }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FRAME_SPRITE);
            if (sprite == null) { Debug.LogError($"[UnlockFlash] sprite 없음: {FRAME_SPRITE}"); return; }

            // 프레임 Image 자식(멱등)
            var frameTr = cell.Find(FRAME_NAME);
            Image frameImg;
            if (frameTr == null)
            {
                var go = new GameObject(FRAME_NAME, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameTr = go.transform as RectTransform;
                frameTr.SetParent(cell, false);
                frameTr.SetAsLastSibling(); // 셀 최상위(아이콘/이름/잠금 위에서 테두리 반짝)
                StretchFull((RectTransform)frameTr);
                frameImg = go.GetComponent<Image>();
            }
            else
            {
                frameImg = frameTr.GetComponent<Image>();
                ((RectTransform)frameTr).SetAsLastSibling();
            }

            frameImg.sprite = sprite;
            frameImg.type = Image.Type.Sliced; // 9-slice 테두리
            frameImg.raycastTarget = false;
            // 양피지 빛바랜 톤(text-bone 근사) — 화사한 골드/네온 금지(기획 §8.3)
            frameImg.color = new Color(0.792f, 0.749f, 0.651f, 0f); // a=0 (반짝 코루틴이 0→1→0)

            // CraftScrollCell.m_UnlockFlashFrame 슬롯 연결
            var cellComp = cell.GetComponent<CraftScrollCell>();
            if (cellComp == null) { Debug.LogError("[UnlockFlash] CraftScrollCell 컴포넌트 없음."); return; }
            var so = new SerializedObject(cellComp);
            var prop = so.FindProperty("m_UnlockFlashFrame");
            if (prop == null) { Debug.LogError("[UnlockFlash] m_UnlockFlashFrame 프로퍼티 없음."); return; }
            prop.objectReferenceValue = frameImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
            Debug.Log("[UnlockFlash] CraftScrollCell.UnlockFlashFrame 자식 추가 + 슬롯 연결 + 프리팹 저장 완료.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }
}
