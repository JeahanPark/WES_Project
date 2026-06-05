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

    // 해금 상태 변경 시 현재 표시 중인 셀들의 잠금 오버레이를 즉시 갱신한다(목록 재배치 없음).
    public void RefreshLockStates()
    {
        var list = GetDataList();
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            var cell = GetCellAtIndex(i) as CraftScrollCell;
            cell?.RefreshLockState();
        }
    }

    // 방금 해금된 craftId가 현재 목록에 보이면 그 셀만 해금 반짝 연출. 안 보이면 무시(다음 표시 시 정상색).
    public void PlayUnlockFlashFor(int _craftId)
    {
        var list = GetDataList();
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || list[i].Id != _craftId)
                continue;

            var cell = GetCellAtIndex(i) as CraftScrollCell;
            cell?.PlayUnlockFlash();
            return;
        }
    }
}
