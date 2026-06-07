using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 개별 퀵슬롯 칸 UI
/// </summary>
public class QuickSlotCell : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    private static readonly Color SLOT_BORDER_COLOR = new Color(0.4f, 0.4f, 0.5f, 1f);
    private static readonly Color KEY_NUMBER_COLOR = new Color(1f, 0.9f, 0.5f, 1f);

    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private TextMeshProUGUI m_KeyNumberText;
    [SerializeField] private Image m_DimOverlay;

    private int m_SlotIndex;
    private int m_ItemInfoId;
    private Outline m_Outline;

    public int SlotIndex => m_SlotIndex;

    private void Awake()
    {
        ApplyStyle();
    }

    public void Setup(int _slotIndex)
    {
        m_SlotIndex = _slotIndex;

        if (m_KeyNumberText != null)
            m_KeyNumberText.text = (_slotIndex + 1).ToString();

        SetEmpty();
    }

    private void ApplyStyle()
    {
        // 셀 루트 Image는 slot_frame(나무 프레임) sprite를 갖는다.
        // 과거엔 여기에 SLOT_BG_COLOR(다크블루 0.85)를 곱해 프레임이 죽었다(C-3).
        // 인벤·제작 셀과 동일한 나무 프레임 톤을 위해 화이트로 유지해 sprite 원톤을 살린다.
        var bgImage = GetComponent<Image>();
        if (bgImage != null)
            bgImage.color = Color.white;

        // 테두리 추가
        m_Outline = GetComponent<Outline>();
        if (m_Outline == null)
            m_Outline = gameObject.AddComponent<Outline>();
        m_Outline.effectColor = SLOT_BORDER_COLOR;
        m_Outline.effectDistance = new Vector2(2, -2);

        // 키 번호 스타일
        if (m_KeyNumberText != null)
        {
            m_KeyNumberText.color = KEY_NUMBER_COLOR;
            m_KeyNumberText.fontSize = 16;
            m_KeyNumberText.fontStyle = FontStyles.Bold;
        }
    }

    public void UpdateSlot(int _itemInfoId, InventoryRegistry _inventory)
    {
        m_ItemInfoId = _itemInfoId;

        if (_itemInfoId == 0)
        {
            SetEmpty();
            return;
        }

        ItemInfo info = Managers.Info.ItemInfoList.Find(i => i.Id == _itemInfoId);
        if (info == null)
        {
            SetEmpty();
            return;
        }

        // 아이콘 표시
        if (m_IconImage != null)
        {
            m_IconImage.sprite = !string.IsNullOrEmpty(info.IconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(info.IconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }

        // 인벤토리 보유 수량 표시
        ItemData itemData = _inventory?.GetItem(_itemInfoId);
        int count = itemData?.Count ?? 0;

        if (m_CountText != null)
        {
            m_CountText.text = count > 0 ? count.ToString() : "0";
        }

        // 인벤토리에 없으면 어둡게
        if (m_DimOverlay != null)
        {
            m_DimOverlay.gameObject.SetActive(count <= 0);
        }
    }

    private void SetEmpty()
    {
        if (m_IconImage != null)
        {
            m_IconImage.sprite = null;
            m_IconImage.enabled = false;
        }

        if (m_CountText != null)
            m_CountText.text = string.Empty;

        if (m_DimOverlay != null)
            m_DimOverlay.gameObject.SetActive(false);
    }

    // 인벤토리에서 드래그하여 퀵슬롯에 드롭
    public void OnDrop(PointerEventData _eventData)
    {
        GameDebug.Log($"[QuickSlotCell] OnDrop slot={m_SlotIndex} pointerDrag={_eventData.pointerDrag?.name ?? "null"}");
        var draggedCell = _eventData.pointerDrag?.GetComponent<InventoryScrollCell>();
        if (draggedCell == null || draggedCell.ItemData == null)
        {
            GameDebug.LogWarning($"[QuickSlotCell] OnDrop 차단: draggedCell={draggedCell}, itemData={draggedCell?.ItemData}");
            return;
        }

        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        objectData.GetQuickSlotRegistry().Register(m_SlotIndex, draggedCell.ItemData.Info.Id);
        GameDebug.Log($"[QuickSlotCell] 퀵슬롯 등록: slot={m_SlotIndex}, itemId={draggedCell.ItemData.Info.Id}");
    }

    // 우클릭으로 퀵슬롯 해제
    public void OnPointerClick(PointerEventData _eventData)
    {
        if (_eventData.button != PointerEventData.InputButton.Right)
            return;

        if (m_ItemInfoId == 0)
            return;

        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        objectData.GetQuickSlotRegistry().Unregister(m_SlotIndex);
    }
}
