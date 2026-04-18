using UnityEngine;

/// <summary>
/// 퀵슬롯 HUD 전체 관리
/// </summary>
public class QuickSlotHUD : MonoBehaviour
{
    private QuickSlotCell[] m_Cells;

    private QuickSlotRegistry m_QuickSlotRegistry;
    private InventoryRegistry m_InventoryRegistry;

    public void Initialize(QuickSlotRegistry _quickSlotRegistry, InventoryRegistry _inventoryRegistry)
    {
        m_QuickSlotRegistry = _quickSlotRegistry;
        m_InventoryRegistry = _inventoryRegistry;

        m_Cells = GetComponentsInChildren<QuickSlotCell>(true);

        for (int i = 0; i < m_Cells.Length; i++)
        {
            m_Cells[i].Setup(i);
        }

        m_QuickSlotRegistry.OnSlotChanged += OnSlotChanged;
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (m_QuickSlotRegistry != null)
            m_QuickSlotRegistry.OnSlotChanged -= OnSlotChanged;
    }

    private void OnSlotChanged(int _slotIndex)
    {
        RefreshSlot(_slotIndex);
    }

    public void RefreshSlot(int _slotIndex)
    {
        if (_slotIndex < 0 || _slotIndex >= m_Cells.Length)
            return;

        int itemInfoId = m_QuickSlotRegistry.GetItemInfoId(_slotIndex);
        m_Cells[_slotIndex].UpdateSlot(itemInfoId, m_InventoryRegistry);
    }

    public void RefreshAll()
    {
        for (int i = 0; i < m_Cells.Length; i++)
        {
            RefreshSlot(i);
        }
    }
}
