using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 월드에 드랍된 아이템 오브젝트.
/// 서버에서 스폰되며, 플레이어가 상호작용 시 인벤토리에 추가되고 Despawn된다.
/// </summary>
public class WorldDropItem : WorldBaseObject
{
    private NetworkVariable<int> m_ItemInfoId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> m_Count = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private ItemInfo m_ItemInfo;

    public ItemInfo ItemInfo => m_ItemInfo;
    public int Count => m_Count.Value;
    // NetworkVariable 원본 itemInfoId (Read=Everyone) — 클론에서도 양측 동일값 보장.
    // 캐싱된 m_ItemInfo(단측 Load 결과)가 아니라 이 값을 QA 스냅샷 대조에 쓴다.
    public int ItemInfoId => m_ItemInfoId.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        m_ItemInfoId.OnValueChanged += OnItemInfoIdChanged;
        LoadItemInfo();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        m_ItemInfoId.OnValueChanged -= OnItemInfoIdChanged;
    }

    /// <summary>
    /// 서버에서 스폰 직후 아이템 정보를 초기화한다.
    /// </summary>
    public void Initialize(int _itemInfoId, int _count)
    {
        if (!IsServer)
            return;

        m_ItemInfoId.Value = _itemInfoId;
        m_Count.Value = _count;
    }

    /// <summary>
    /// 플레이어가 수집 요청 (클라이언트 → 서버)
    /// </summary>
    [Rpc(SendTo.Server)]
    public void CollectServerRpc(RpcParams _rpcParams = default)
    {
        if (!IsSpawned)
            return;

        int itemInfoId = m_ItemInfoId.Value;
        int count = m_Count.Value;
        ulong senderClientId = _rpcParams.Receive.SenderClientId;

        AddItemClientRpc(itemInfoId, count, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));

        GameDebug.Log($"[WorldDropItem] Collected: ItemId={itemInfoId}, Count={count}");

        NetworkObject.Despawn();
    }

    // sender 단독 적용. 도면 아이템이면 인벤토리 미경유로 레시피 해금, 아니면 인벤토리 추가.
    [Rpc(SendTo.SpecifiedInParams)]
    private void AddItemClientRpc(int _itemInfoId, int _count, RpcParams _rpcParams = default)
    {
        var objectData = InGameController.Instance?.ObjectDataWorker;
        if (objectData == null)
            return;

        BlueprintInfo blueprint = Managers.Info.GetBlueprintByItemId(_itemInfoId);
        if (blueprint != null)
        {
            // 도면: 슬롯 미점유 즉시 해금. 인벤토리 추가 안 함.
            objectData.GetRecipeUnlockRegistry().Unlock(blueprint.UnlockCraftId);
            return;
        }

        objectData.GetInventoryRegistry().AddItem(_itemInfoId, _count);
    }

    private void LoadItemInfo()
    {
        if (m_ItemInfoId.Value == 0)
            return;

        m_ItemInfo = Managers.Info.ItemInfoList.Find(x => x.Id == m_ItemInfoId.Value);
    }

    private void OnItemInfoIdChanged(int _prev, int _current)
    {
        LoadItemInfo();
    }
}
