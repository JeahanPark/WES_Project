using System.Collections.Generic;

/// <summary>
/// 인벤토리 데이터 관리 (로컬)
/// </summary>
public class InventoryRegistry
{
    private List<ItemData> m_Items = new();

    public void AddItem(int _infoId, int _count = 1)
    {
        ItemInfo info = Managers.Info.ItemInfoList.Find(i => i.Id == _infoId);
        if (info == null)
            return;

        if (info.IsStackable)
        {
            ItemData existing = m_Items.Find(item => item.Info.Id == _infoId);
            if (existing != null)
            {
                existing.AddCount(_count);
                return;
            }
        }

        m_Items.Add(new ItemData(info, _count));
    }

    public bool RemoveItem(int _infoId, int _count = 1)
    {
        ItemData item = m_Items.Find(i => i.Info.Id == _infoId);
        if (item == null)
            return false;

        if (item.Count < _count)
            return false;

        item.AddCount(-_count);

        if (item.Count <= 0)
        {
            m_Items.Remove(item);
        }

        return true;
    }

    public ItemData GetItem(int _infoId)
    {
        return m_Items.Find(item => item.Info.Id == _infoId);
    }

    public List<ItemData> GetAllItems()
    {
        return m_Items;
    }

    public void Clear()
    {
        m_Items.Clear();
    }
}
