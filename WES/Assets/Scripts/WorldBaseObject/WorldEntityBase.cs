using UnityEngine;

/// <summary>
/// 월드에 존재하며 드랍 로직을 가질 수 있는 오브젝트 베이스.
/// m_SourceId가 0이면 드랍 없음 (Player 등).
/// </summary>
public class WorldEntityBase : WorldBaseObject
{
    [SerializeField] private WorldObjectType m_WorldObjectType;
    [SerializeField] private int m_SourceId;

    /// <summary>
    /// 사망/파괴 시 서버에서 호출. DropSourceInfo → DropTableItemInfo 조회 후 아이템 스폰.
    /// </summary>
    protected void ExecuteDrop(Vector3 _position)
    {
        if (m_SourceId == 0)
            return;

        var dropSource = Managers.Info.DropSourceInfoList.Find(
            x => x.WorldObjectType == m_WorldObjectType && x.SourceId == m_SourceId);

        if (dropSource == null)
        {
            GameDebug.LogWarning($"[WorldEntityBase] DropSource not found: Type={m_WorldObjectType}, SourceId={m_SourceId}");
            return;
        }

        var dropItems = Managers.Info.DropTableItemInfoList.FindAll(
            x => x.DropTableId == dropSource.DropTableId);

        foreach (var dropItem in dropItems)
        {
            if (dropItem.RewardType != RewardType.Item)
                continue;

            int count = Random.Range(dropItem.Min, dropItem.Max + 1);
            if (count <= 0)
                continue;

            // ① 도구 등급: 채집(WorldObject)일 때 보유 최대 도구 등급으로 수확량 증가(CORE §4.1).
            // 전투(Monster) 드롭은 도구 무관 — 채집만 스케일. 도구 없으면 배수 1.0.
            if (m_WorldObjectType == WorldObjectType.WorldObject)
            {
                float toolMul = ToolTierSystem.GatheringMultiplier(ToolTierSystem.GetCurrentToolTier());
                count = Mathf.Max(1, Mathf.RoundToInt(count * toolMul));
            }

            Vector3 spawnPos = _position + new Vector3(
                Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));

            InGameController.Instance.PlayWorker.SpawnDropItem(dropItem.RewardId, count, spawnPos);
        }
    }
}
