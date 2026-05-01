using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryScrollCell : BaseScrollCell<ItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    private static readonly Color SLOT_EMPTY_COLOR = new Color(0.18f, 0.18f, 0.22f, 0.85f);
    private static readonly Color SLOT_FILLED_COLOR = new Color(0.25f, 0.25f, 0.30f, 0.95f);
    private static readonly Color SLOT_BORDER_COLOR = new Color(0.4f, 0.4f, 0.5f, 1f);

    [SerializeField] private Image m_IconImage;
    [SerializeField] private TextMeshProUGUI m_CountText;
    [SerializeField] private Image m_BackgroundImage;
    [SerializeField] private GameObject m_SelectedFrame;

    private ItemData m_ItemData;
    private bool m_IsSelected;
    private Outline m_Outline;

    public ItemData ItemData => m_ItemData;

    private void Awake()
    {
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        var bg = m_BackgroundImage != null ? m_BackgroundImage : GetComponent<Image>();
        if (bg != null)
            bg.color = SLOT_EMPTY_COLOR;

        m_Outline = GetComponent<Outline>();
        if (m_Outline == null)
            m_Outline = gameObject.AddComponent<Outline>();
        m_Outline.effectColor = SLOT_BORDER_COLOR;
        m_Outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void RefreshBackgroundColor(bool _hasItem)
    {
        var bg = m_BackgroundImage != null ? m_BackgroundImage : GetComponent<Image>();
        if (bg != null)
            bg.color = _hasItem ? SLOT_FILLED_COLOR : SLOT_EMPTY_COLOR;
    }

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

        RefreshBackgroundColor(true);
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
        RefreshBackgroundColor(false);
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

    // P11: 더블클릭으로 직접 설치/사용
    public void OnPointerClick(PointerEventData _eventData)
    {
        if (m_ItemData == null || m_ItemData.Info == null)
            return;

        if (_eventData.button != PointerEventData.InputButton.Left)
            return;

        if (_eventData.clickCount < 2)
            return;

        // 건물 아이템: 즉시 배치 모드 진입 + 인벤토리 팝업 닫기
        if (m_ItemData.Info.IsBuilding)
        {
            InGameController.Instance?.BuildingPlacementWorker?.StartPlacement(m_ItemData.Info.Id);
            var popup = GetComponentInParent<InventoryPopup>(true);
            popup?.Close();
        }
    }
}
