/// <summary>
/// 인벤토리에 있는 아이템 인스턴스
/// </summary>
public class ItemData
{
    private ItemInfo m_Info;
    private int m_Count;

    public ItemInfo Info => m_Info;
    public int Count => m_Count;

    public ItemData(ItemInfo _info, int _count = 1)
    {
        m_Info = _info;
        m_Count = _count;
    }

    public void AddCount(int _amount)
    {
        if (m_Info == null)
            return;

        m_Count += _amount;

        if (m_Info.IsStackable && m_Count > m_Info.MaxStack)
        {
            m_Count = m_Info.MaxStack;
        }
    }

    public void SetCount(int _count)
    {
        m_Count = _count;
    }
}
