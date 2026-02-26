using System;
using System.Collections.Generic;

public class InventoryScroll : BaseScroll<ItemData>
{
    private Action<ItemData> m_OnCellClickCallback;

    protected override void OnAwake()
    {
        base.OnAwake();
    }

    public void SetCellClickCallback(Action<ItemData> _callback)
    {
        m_OnCellClickCallback = _callback;
    }

    public void NotifyCellClicked(ItemData _itemData)
    {
        m_OnCellClickCallback?.Invoke(_itemData);
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
