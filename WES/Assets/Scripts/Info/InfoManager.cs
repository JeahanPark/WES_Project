using System.Collections.Generic;

public partial class InfoManager : Singleton<InfoManager>
{
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
