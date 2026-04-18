using System.Collections.Generic;
using UnityEngine;

public class InventoryPopup : BasePopup
{
    [SerializeField] private InventoryScroll m_InventoryScroll;
    [SerializeField] private InventoryDetailPanel m_DetailPanel;

    private void Start()
    {
        m_InventoryScroll.SetCellClickCallback(OnCellClicked);
        RefreshInventory();
    }

    public void RefreshInventory()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        ItemData[] slots = inventory.GetSlots();

        // BaseScroll은 List<TData>를 받으므로, 배열을 List로 변환 (null 포함)
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
}
