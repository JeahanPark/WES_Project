using System;
using System.Collections.Generic;

public class CraftScroll : BaseScroll<CraftInfo>
{
    private Action<CraftInfo> m_OnCellClickCallback;

    public void SetCellClickCallback(Action<CraftInfo> _callback)
    {
        m_OnCellClickCallback = _callback;
    }

    public void NotifyCellClicked(CraftInfo _craftInfo)
    {
        m_OnCellClickCallback?.Invoke(_craftInfo);
    }
}
