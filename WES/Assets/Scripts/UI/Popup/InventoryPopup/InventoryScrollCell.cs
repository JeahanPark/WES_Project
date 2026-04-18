using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryScrollCell : BaseScrollCell<ItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private Image m_BackgroundImage;
    [SerializeField] private GameObject m_SelectedFrame;

    private ItemData m_ItemData;
    private bool m_IsSelected;

    public ItemData ItemData => m_ItemData;

    protected override void OnUpdateCell(int _index, ItemData _data)
    {
        m_ItemData = _data;

        if (m_ItemData == null || m_ItemData.Info == null)
        {
            SetEmpty();
            return;
        }

        if (m_CountText != null)
        {
            m_CountText.text = m_ItemData.Count > 1 ? m_ItemData.Count.ToString() : string.Empty;
        }

        if (m_IconImage != null)
        {
            string iconKey = m_ItemData.Info.IconKey;
            m_IconImage.sprite = !string.IsNullOrEmpty(iconKey)
                ? Managers.Resource.LoadAddressable<Sprite>(iconKey)
                : null;
            m_IconImage.enabled = m_IconImage.sprite != null;
        }
    }

    private void SetEmpty()
    {
        if (m_CountText != null)
        {
            m_CountText.text = string.Empty;
        }

        if (m_IconImage != null)
        {
            m_IconImage.sprite = null;
            m_IconImage.enabled = false;
        }

        SetSelected(false);
    }

    public void SetSelected(bool _selected)
    {
        m_IsSelected = _selected;
        if (m_SelectedFrame != null)
            m_SelectedFrame.SetActive(_selected);
    }

    public void OnClickCell()
    {
        if (m_ItemData == null)
            return;

        var scroll = GetComponentInParent<InventoryScroll>(true);
        scroll?.NotifyCellClicked(m_ItemData);
    }

    // ===== 드래그 앤 드롭 =====

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (m_ItemData == null)
        {
            _eventData.pointerDrag = null;
            return;
        }

        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.BeginDrag(this, _eventData);

        // 원래 셀 반투명 처리
        if (m_IconImage != null)
            m_IconImage.color = new Color(1f, 1f, 1f, 0.3f);
    }

    public void OnDrag(PointerEventData _eventData)
    {
        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.UpdateDrag(_eventData);
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.EndDrag();

        // 원래 셀 불투명 복원
        if (m_IconImage != null)
            m_IconImage.color = Color.white;
    }

    public void OnDrop(PointerEventData _eventData)
    {
        var popup = GetComponentInParent<InventoryPopup>(true);
        popup?.DropOnSlot(this);
    }
}
