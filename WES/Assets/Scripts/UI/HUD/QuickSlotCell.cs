using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 개별 퀵슬롯 칸 UI
/// </summary>
public class QuickSlotCell : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private TextMeshProUGUI m_KeyNumberText;
    [SerializeField] private Image m_DimOverlay;

    private int m_SlotIndex;
    private int m_ItemInfoId;

    public int SlotIndex => m_SlotIndex;

    public void Setup(int _slotIndex)
    {
        m_SlotIndex = _slotIndex;

        if (m_KeyNumberText != null)
            m_KeyNumberText.text = (_slotIndex + 1).ToString();

        SetEmpty();
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
        var draggedCell = _eventData.pointerDrag?.GetComponent<InventoryScrollCell>();
        if (draggedCell == null || draggedCell.ItemData == null)
            return;

        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        objectData.GetQuickSlotRegistry().Register(m_SlotIndex, draggedCell.ItemData.Info.Id);
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
