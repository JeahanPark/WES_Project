using System.Collections.Generic;

// 도구 등급(① 효율, CORE §4.1) — 보유한 도구 등급이 높을수록 채집 수확량 증가.
// R1: 시스템 토대만. 실제 등급별 도구 아이템(나무/돌/철 도끼 등 ItemInfo.ToolTier>0)과
// 수치 튜닝은 R3 콘텐츠·R5. 도구가 없으면 tier 0 = 배수 1.0(기존과 동일, 비파괴).
public static class ToolTierSystem
{
    // 채집 수확 배수 = 1 + 0.5·tier (R5 튜닝). tier0=1.0 / tier1=1.5 / tier2=2.0 / tier3=2.5
    public static float GatheringMultiplier(int _tier)
    {
        if (_tier <= 0)
            return 1f;
        return 1f + 0.5f * _tier;
    }

    // 현재 인벤토리에서 보유한 최대 도구 등급(ItemInfo.ToolTier). 도구 없으면 0.
    public static int GetCurrentToolTier()
    {
        var inv = InGameController.Instance?.ObjectDataWorker?.GetInventoryRegistry();
        if (inv == null)
            return 0;

        int max = 0;
        List<ItemData> items = inv.GetAllItems();
        for (int i = 0; i < items.Count; i++)
        {
            ItemInfo info = items[i]?.Info;
            if (info != null && info.ToolTier > max)
                max = info.ToolTier;
        }
        return max;
    }
}
