using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public partial class InfoManager : Singleton<InfoManager>
{
    private bool m_IsInfoLoaded = false;

    public bool IsInfoLoaded => m_IsInfoLoaded;

    // 진입 경로(MPPM Host / TestMode / 일반)와 무관하게 1회만 로드 보장하는 멱등 래퍼.
    public async UniTask LoadAllInfoOnce()
    {
        if (m_IsInfoLoaded)
            return;

        await LoadAllInfo();
        m_IsInfoLoaded = true;
    }

    public List<CraftInfo> GetCraftInfosByCategory(CraftCategoryType _type)
    {
        return CraftInfoList.FindAll(_info => _info.CategoryType == _type);
    }

    public List<CraftMaterialInfo> GetMaterialsByCraftId(int _craftId)
    {
        return CraftMaterialInfoList.FindAll(_info => _info.CraftId == _craftId);
    }

    public List<CraftConditionInfo> GetConditionsByCraftId(int _craftId)
    {
        return CraftConditionInfoList.FindAll(_info => _info.CraftId == _craftId);
    }

    // 도면 아이템 Id로 도면 매핑 조회. 도면이 아니면 null.
    public BlueprintInfo GetBlueprintByItemId(int _blueprintItemId)
    {
        return BlueprintInfoList.Find(_info => _info.BlueprintItemId == _blueprintItemId);
    }

    // 레시피(CraftId)가 도면 해금 대상인지 조회. 대상이 아니면 null(= 기본 상시 레시피).
    public BlueprintInfo GetBlueprintByCraftId(int _unlockCraftId)
    {
        return BlueprintInfoList.Find(_info => _info.UnlockCraftId == _unlockCraftId);
    }

    // 해당 CraftId가 도면으로 잠긴 레시피인지 여부.
    public bool IsBlueprintLockedCraft(int _craftId)
    {
        return GetBlueprintByCraftId(_craftId) != null;
    }
}
