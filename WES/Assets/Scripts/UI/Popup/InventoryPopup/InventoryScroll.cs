using System.Collections.Generic;

public class InventoryScroll : BaseScroll<ItemData>
{
    protected override void OnAwake()
    {
        base.OnAwake();
    }

    public void Refresh()
    {
        if (InGameController.Instance == null || InGameController.Instance.ObjectDataWorker == null)
            return;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        var items = inventory.GetAllItems();

        SetData(items);
    }
}
