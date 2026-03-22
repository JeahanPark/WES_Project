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
    public void CollectServerRpc()
    {
        if (!IsSpawned)
            return;

        var inventory = InGameController.Instance.ObjectDataWorker.GetInventoryRegistry();
        inventory.AddItem(m_ItemInfoId.Value, m_Count.Value);

        GameDebug.Log($"[WorldDropItem] Collected: ItemId={m_ItemInfoId.Value}, Count={m_Count.Value}");

        NetworkObject.Despawn();
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
