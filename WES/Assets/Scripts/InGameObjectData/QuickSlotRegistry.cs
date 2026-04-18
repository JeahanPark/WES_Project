using System;

/// <summary>
/// 퀵슬롯 8칸 데이터 관리
/// 아이템의 InfoId를 저장하여 인벤토리와 연결
/// </summary>
public class QuickSlotRegistry
{
    public const int SLOT_COUNT = 8;

    private int[] m_SlotItemInfoIds;

    public event Action<int> OnSlotChanged;

    public QuickSlotRegistry()
    {
        m_SlotItemInfoIds = new int[SLOT_COUNT];
    }

    public void Register(int _slotIndex, int _itemInfoId)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return;

        // 이미 다른 슬롯에 같은 아이템이 등록되어 있으면 해제
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (m_SlotItemInfoIds[i] == _itemInfoId)
            {
                m_SlotItemInfoIds[i] = 0;
                OnSlotChanged?.Invoke(i);
            }
        }

        m_SlotItemInfoIds[_slotIndex] = _itemInfoId;
        OnSlotChanged?.Invoke(_slotIndex);
    }

    public void Unregister(int _slotIndex)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return;

        m_SlotItemInfoIds[_slotIndex] = 0;
        OnSlotChanged?.Invoke(_slotIndex);
    }

    public int GetItemInfoId(int _slotIndex)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return 0;

        return m_SlotItemInfoIds[_slotIndex];
    }

    public void SwapSlots(int _fromIndex, int _toIndex)
    {
        if (_fromIndex < 0 || _fromIndex >= SLOT_COUNT)
            return;
        if (_toIndex < 0 || _toIndex >= SLOT_COUNT)
            return;

        (m_SlotItemInfoIds[_fromIndex], m_SlotItemInfoIds[_toIndex]) =
            (m_SlotItemInfoIds[_toIndex], m_SlotItemInfoIds[_fromIndex]);

        OnSlotChanged?.Invoke(_fromIndex);
        OnSlotChanged?.Invoke(_toIndex);
    }

    /// <summary>
    /// 퀵슬롯 아이템 사용
    /// </summary>
    public void UseSlot(int _slotIndex, InventoryRegistry _inventory)
    {
        if (_slotIndex < 0 || _slotIndex >= SLOT_COUNT)
            return;

        int itemInfoId = m_SlotItemInfoIds[_slotIndex];
        if (itemInfoId == 0)
            return;

        // 인벤토리에 해당 아이템이 있는지 확인
        ItemData itemData = _inventory.GetItem(itemInfoId);
        if (itemData == null)
            return;

        // 건물 아이템: 배치 모드 진입
        if (itemData.Info.IsBuilding)
        {
            InGameController.Instance?.BuildingPlacementWorker?.StartPlacement(itemData.Info.Id);
            return;
        }

        // 소비 아이템: 사용 + 차감 (추후 아이템 타입별 효과 확장)
    }

    public void Clear()
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            m_SlotItemInfoIds[i] = 0;
        }
    }
}
