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
}
