using System.Collections.Generic;

/// <summary>
/// 레시피 도면 해금 상태 관리 (현재 세션·현재 클라이언트 로컬).
/// 직렬화/영구 저장 없음. 세션 종료 시 Clear로 초기화(영구 진행 안티골 준수).
/// 기본 상시 레시피(도면 매핑이 없는 CraftId)는 항상 해금으로 본다.
/// </summary>
public class RecipeUnlockRegistry
{
    private HashSet<int> m_UnlockedCraftIds = new HashSet<int>();

    // 해금된 CraftId 전달. 신규 해금일 때만 발화.
    public event System.Action<int> OnUnlockChanged;

    /// <summary>
    /// 해당 레시피가 제작 가능 상태인지.
    /// 도면 매핑이 없는 CraftId(기본 5종)는 항상 true.
    /// 도면 잠금 대상이면 해금 집합 포함 여부.
    /// </summary>
    public bool IsUnlocked(int _craftId)
    {
        if (!Managers.Info.IsBlueprintLockedCraft(_craftId))
            return true;

        return m_UnlockedCraftIds.Contains(_craftId);
    }

    /// <summary>
    /// 도면을 통해 레시피를 해금한다. 신규일 때만 이벤트 발화.
    /// </summary>
    public void Unlock(int _craftId)
    {
        if (m_UnlockedCraftIds.Add(_craftId))
            OnUnlockChanged?.Invoke(_craftId);
    }

    /// <summary>
    /// 세션 리셋. 인벤토리 Clear와 동일 지점에서 호출(영구 진행 방지).
    /// </summary>
    public void Clear()
    {
        m_UnlockedCraftIds.Clear();
    }
}
