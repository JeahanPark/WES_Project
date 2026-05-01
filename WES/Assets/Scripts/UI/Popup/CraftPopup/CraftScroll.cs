using System;
using System.Collections.Generic;
using UnityEngine;

public class CraftScroll : BaseScroll<CraftInfo>
{
    private Action<CraftInfo> m_OnCellClickCallback;
    private CraftScrollCell m_CurrentSelected;

    public void SetCellClickCallback(Action<CraftInfo> _callback)
    {
        m_OnCellClickCallback = _callback;
    }

    public void NotifyCellClicked(CraftInfo _craftInfo, CraftScrollCell _cell)
    {
        if (m_CurrentSelected != null && m_CurrentSelected != _cell)
            m_CurrentSelected.SetSelected(false);

        m_CurrentSelected = _cell;
        if (_cell != null)
            _cell.SetSelected(true);

        m_OnCellClickCallback?.Invoke(_craftInfo);
    }

    public void ClearSelection()
    {
        if (m_CurrentSelected != null)
        {
            m_CurrentSelected.SetSelected(false);
            m_CurrentSelected = null;
        }
    }
}
