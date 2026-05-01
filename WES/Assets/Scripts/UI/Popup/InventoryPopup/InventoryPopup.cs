using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryPopup : BasePopup
{
    [SerializeField] private InventoryScroll m_InventoryScroll;
    [SerializeField] private InventoryDetailPanel m_DetailPanel;
    [SerializeField] private Image m_DragPreview;

    private InventoryScrollCell m_DragSourceCell;
    private InventoryRegistry m_SubscribedRegistry;

    private void Start()
    {
        m_InventoryScroll.SetCellClickCallback(OnCellClicked);
        SubscribeInventory();
        RefreshInventory();

        if (m_DragPreview != null)
            m_DragPreview.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeInventory();
    }

    private void SubscribeInventory()
    {
        var inventory = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inventory == null || m_SubscribedRegistry == inventory)
            return;

        UnsubscribeInventory();
        inventory.OnInventoryChanged += RefreshInventory;
        m_SubscribedRegistry = inventory;
    }

    private void UnsubscribeInventory()
    {
        if (m_SubscribedRegistry != null)
        {
            m_SubscribedRegistry.OnInventoryChanged -= RefreshInventory;
            m_SubscribedRegistry = null;
        }
    }

    public void RefreshInventory()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        ItemData[] slots = inventory.GetSlots();

        var slotList = new List<ItemData>(slots);
        m_InventoryScroll.SetData(slotList);

        m_DetailPanel.gameObject.SetActive(false);
    }

    public void OnClickClose()
    {
        Close();
    }

    private void OnCellClicked(ItemData _itemData)
    {
        if (_itemData == null)
            return;

        m_DetailPanel.gameObject.SetActive(true);
        m_DetailPanel.Show(_itemData);
    }

    // ===== 드래그 앤 드롭 관리 =====

    public void BeginDrag(InventoryScrollCell _sourceCell, PointerEventData _eventData)
    {
        m_DragSourceCell = _sourceCell;

        if (m_DragPreview != null && _sourceCell.ItemData != null)
        {
            m_DragPreview.gameObject.SetActive(true);
            m_DragPreview.sprite = Managers.Resource.LoadAddressable<Sprite>(_sourceCell.ItemData.Info.IconKey);
            m_DragPreview.rectTransform.position = _eventData.position;
        }
    }

    public void UpdateDrag(PointerEventData _eventData)
    {
        if (m_DragPreview != null && m_DragPreview.gameObject.activeSelf)
        {
            m_DragPreview.rectTransform.position = _eventData.position;
        }
    }

    public void EndDrag()
    {
        m_DragSourceCell = null;

        if (m_DragPreview != null)
            m_DragPreview.gameObject.SetActive(false);
    }

    public void DropOnSlot(InventoryScrollCell _targetCell)
    {
        if (m_DragSourceCell == null)
            return;

        if (m_DragSourceCell == _targetCell)
            return;

        int fromIndex = m_DragSourceCell.Index;
        int toIndex = _targetCell.Index;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        inventory.SwapSlots(fromIndex, toIndex);

        RefreshInventory();
    }
}
