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
        var items = inventory.GetAllItems();

        m_InventoryScroll.SetData(items);
    }

    public void OnClickClose()
    {
        Close();
    }

    private void OnCellClicked(ItemData _itemData)
    {
        m_DetailPanel.Show(_itemData);
    }
}
