using System;
using System.Collections.Generic;

/// <summary>
/// 인벤토리 데이터 관리 (슬롯 기반)
/// </summary>
public class InventoryRegistry
{
    public const int DEFAULT_SLOT_COUNT = 20;

    private ItemData[] m_Slots;
    private int m_SlotCount;

    public int SlotCount => m_SlotCount;

    public InventoryRegistry(int _slotCount = DEFAULT_SLOT_COUNT)
    {
        m_SlotCount = _slotCount;
        m_Slots = new ItemData[m_SlotCount];
    }

    public void AddItem(int _infoId, int _count = 1)
    {
        ItemInfo info = Managers.Info.ItemInfoList.Find(i => i.Id == _infoId);
        if (info == null)
            return;

        if (info.IsStackable)
        {
            for (int i = 0; i < m_SlotCount; i++)
            {
                if (m_Slots[i] != null && m_Slots[i].Info.Id == _infoId)
                {
                    m_Slots[i].AddCount(_count);
                    return;
                }
            }
        }

        int emptyIndex = FindEmptySlot();
        if (emptyIndex < 0)
        {
            GameDebug.LogWarning("[InventoryRegistry] 인벤토리가 가득 찼습니다.");
            return;
        }

        m_Slots[emptyIndex] = new ItemData(info, _count);
    }

    public bool RemoveItem(int _infoId, int _count = 1)
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] != null && m_Slots[i].Info.Id == _infoId)
            {
                if (m_Slots[i].Count < _count)
                    return false;

                m_Slots[i].AddCount(-_count);

                if (m_Slots[i].Count <= 0)
                {
                    m_Slots[i] = null;
                }

                return true;
            }
        }

        return false;
    }

    public ItemData GetItem(int _infoId)
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] != null && m_Slots[i].Info.Id == _infoId)
                return m_Slots[i];
        }

        return null;
    }

    public ItemData GetSlot(int _index)
    {
        if (_index < 0 || _index >= m_SlotCount)
            return null;

        return m_Slots[_index];
    }

    public ItemData[] GetSlots()
    {
        return m_Slots;
    }

    /// <summary>
    /// 하위 호환용 — 기존 코드가 List를 기대하는 경우
    /// </summary>
    public List<ItemData> GetAllItems()
    {
        var result = new List<ItemData>();
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] != null)
                result.Add(m_Slots[i]);
        }
        return result;
    }

    public void SwapSlots(int _fromIndex, int _toIndex)
    {
        if (_fromIndex < 0 || _fromIndex >= m_SlotCount)
            return;
        if (_toIndex < 0 || _toIndex >= m_SlotCount)
            return;

        (m_Slots[_fromIndex], m_Slots[_toIndex]) = (m_Slots[_toIndex], m_Slots[_fromIndex]);
    }

    public void ExpandSlots(int _newCount)
    {
        if (_newCount <= m_SlotCount)
            return;

        var newSlots = new ItemData[_newCount];
        Array.Copy(m_Slots, newSlots, m_SlotCount);
        m_Slots = newSlots;
        m_SlotCount = _newCount;
    }

    public void Clear()
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            m_Slots[i] = null;
        }
    }

    private int FindEmptySlot()
    {
        for (int i = 0; i < m_SlotCount; i++)
        {
            if (m_Slots[i] == null)
                return i;
        }
        return -1;
    }
}
