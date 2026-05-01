using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인게임 HUD 좌측에 붙는 사이드 탭 버튼 묶음
/// 건축 버튼은 CraftPopup을 열고, 인벤토리 버튼은 InventoryPopup을 연다
/// </summary>
public class CraftHUDTab : MonoBehaviour
{
    private static readonly Color BUTTON_BG_COLOR = new Color(0.2f, 0.2f, 0.25f, 0.9f);
    private static readonly Color BUTTON_BORDER_COLOR = new Color(0.5f, 0.45f, 0.3f, 1f);
    private static readonly Vector2 BUTTON_SIZE = new Vector2(110f, 44f);
    private const int BUTTON_FONT_SIZE = 18;

    [SerializeField] private Button m_BuildingButton;
    [SerializeField] private Button m_InventoryButton;

    private void Awake()
    {
        if (m_BuildingButton != null)
        {
            m_BuildingButton.onClick.AddListener(OnClickOpenBuilding);
            StyleButton(m_BuildingButton);
        }
        if (m_InventoryButton != null)
        {
            m_InventoryButton.onClick.AddListener(OnClickOpenInventory);
            StyleButton(m_InventoryButton);
        }
    }

    private void StyleButton(Button _button)
    {
        var image = _button.GetComponent<Image>();
        if (image != null)
            image.color = BUTTON_BG_COLOR;

        var outline = _button.GetComponent<Outline>();
        if (outline == null)
            outline = _button.gameObject.AddComponent<Outline>();
        outline.effectColor = BUTTON_BORDER_COLOR;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var rt = _button.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = BUTTON_SIZE;
        }

        var label = _button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.color = new Color(0.95f, 0.85f, 0.6f, 1f);
            label.fontStyle = FontStyles.Bold;
            label.fontSize = BUTTON_FONT_SIZE;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
        }
    }

    public void OnClickOpenBuilding()
    {
        var existing = Managers.Popup.FindOpen<CraftPopup>();
        if (existing != null)
        {
            existing.SelectCategory(CraftCategoryType.Building);
        }
        else
        {
            var popup = Managers.Popup.Open<CraftPopup>();
            if (popup != null)
                popup.SelectCategory(CraftCategoryType.Building);
        }
    }

    public void OnClickOpenInventory()
    {
        var existing = Managers.Popup.FindOpen<InventoryPopup>();
        if (existing == null)
            Managers.Popup.Open<InventoryPopup>();
    }
}
