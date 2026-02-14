using UnityEngine;

public class InventoryPopup : BasePopup
{
    [SerializeField] private InventoryScroll m_InventoryScroll;

    private void Start()
    {
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
}
