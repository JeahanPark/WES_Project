using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 게임 진행을 관리하는 Worker
/// - 플레이어/아이템 스폰
/// </summary>
public class InGamePlayWorker : NetworkBehaviour
{
    private const string PLAYER_PREFAB_KEY = "PlayerCharacter";
    private const string WORLD_DROP_ITEM_PREFAB_KEY = "WorldDropItem";
    private const float BUILDING_PLACEMENT_MAX_DISTANCE = 8f;
    private const float BUILDING_OVERLAP_RADIUS = 1.0f;
    private const float SPAWN_SAMPLE_RADIUS = 10f;
    private const float SPAWN_OFFSET_DISTANCE = 2.5f;


    [Header("Spawn Points")]
    [SerializeField] private Transform[] m_PlayerSpawnPoints;

    private PlayerCharacter m_LocalPlayer;

    public PlayerCharacter LocalPlayer => m_LocalPlayer;

    /// <summary>
    /// 게임 시작 시 서버에서 호출
    /// </summary>
    public void StartGame()
    {
        if (!Managers.Network.IsServer)
            return;

        SpawnAllPlayers();
    }

    /// <summary>
    /// 로컬 플레이어 등록 (PlayerCharacter에서 호출)
    /// </summary>
    public void RegisterLocalPlayer(PlayerCharacter _player)
    {
        m_LocalPlayer = _player;

        GameDebug.Log($"[InGamePlayWorker] Local player registered: PlayerIndex {_player.GetPlayerIndex()}");
    }

    public void SpawnBuilding(int _buildingInfoId, Vector3 _position)
    {
        SpawnBuildingServerRpc(_buildingInfoId, _position);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SpawnBuildingServerRpc(int _buildingInfoId, Vector3 _position, RpcParams _rpcParams = default)
    {
        ulong senderId = _rpcParams.Receive.SenderClientId;

        var buildingInfo = Managers.Info.BuildingInfoList.Find(x => x.Id == _buildingInfoId);
        if (buildingInfo == null)
        {
            GameDebug.LogError($"[InGamePlayWorker:server] BuildingInfo not found: {_buildingInfoId} (sender={senderId})");
            return;
        }

        // P7: 요청 클라이언트의 캐릭터 위치 기준으로 배치 거리 검증
        if (!IsPlacementWithinPlayerRange(senderId, _position))
        {
            GameDebug.LogWarning($"[InGamePlayWorker:server] 배치 거리 초과 - 요청 거부 (sender={senderId}, pos={_position})");
            return;
        }

        // P7: 서버측 중복 설치 방지
        if (HasBlockingObject(_position))
        {
            GameDebug.LogWarning($"[InGamePlayWorker:server] 배치 위치 중복 - 요청 거부 (sender={senderId}, pos={_position})");
            return;
        }

        var networkObj = InGameController.Instance.SpawnWorker.SpawnObject(buildingInfo.PrefabKey, _position);
        var buildingObject = networkObj != null ? networkObj.GetComponent<WorldBuildingObject>() : null;
        buildingObject?.SetBuildingInfoId(_buildingInfoId);
    }

    private bool IsPlacementWithinPlayerRange(ulong _senderClientId, Vector3 _position)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(_senderClientId, out var clientData))
            return false;

        var playerObj = clientData.PlayerObject;
        if (playerObj == null)
            return false;

        float dist = Vector3.Distance(playerObj.transform.position, _position);
        return dist <= BUILDING_PLACEMENT_MAX_DISTANCE;
    }

    private bool HasBlockingObject(Vector3 _position)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int mask = ~(1 << groundLayer);
        var hits = Physics.OverlapSphere(_position, BUILDING_OVERLAP_RADIUS, mask, QueryTriggerInteraction.Ignore);
        return hits != null && hits.Length > 0;
    }

    public void SpawnDropItem(int _itemInfoId, int _count, Vector3 _position)
    {
        if (!Managers.Network.IsServer)
            return;

        var itemInfo = Managers.Info.ItemInfoList.Find(x => x.Id == _itemInfoId);
        if (itemInfo == null)
        {
            GameDebug.LogError($"[InGamePlayWorker] ItemInfo not found: {_itemInfoId}");
            return;
        }

        string prefabKey = string.IsNullOrEmpty(itemInfo.PrefabKey) ? WORLD_DROP_ITEM_PREFAB_KEY : itemInfo.PrefabKey;

        var dropItem = InGameController.Instance.SpawnWorker.SpawnObject<WorldDropItem>(prefabKey, _position);
        dropItem?.Initialize(_itemInfoId, _count);
    }

    private void SpawnAllPlayers()
    {
        if (!Managers.Network.IsServer)
            return;

        GameObject playerPrefab = Managers.Resource.LoadAddressable<GameObject>(PLAYER_PREFAB_KEY);
        if (playerPrefab == null)
        {
            GameDebug.LogError($"[InGamePlayWorker] Player prefab not found: {PLAYER_PREFAB_KEY}");
            return;
        }

        ulong[] clientIds = Managers.Network.GetConnectedClientIds();
        int playerIndex = 0;

        Vector3 basePosition = GetPlayerSpawnPosition(0);

        foreach (ulong clientId in clientIds)
        {
            Vector3 spawnPosition = GetValidSpawnPosition(GetSpawnOffset(basePosition, playerIndex));

            GameObject instance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            instance.SetActive(true);

            PlayerCharacter player = instance.GetComponent<PlayerCharacter>();
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();

            if (networkObject != null && player != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                player.SetPlayerIndex(playerIndex);

                GameDebug.Log($"[InGamePlayWorker] Spawned player {playerIndex} for client {clientId}");
            }

            playerIndex++;
        }

        GameDebug.Log($"[InGamePlayWorker] All players spawned! Total: {playerIndex}");
    }

    private Vector3 GetPlayerSpawnPosition(int _index)
    {
        if (m_PlayerSpawnPoints != null && m_PlayerSpawnPoints.Length > 0)
        {
            int spawnIndex = _index % m_PlayerSpawnPoints.Length;
            return m_PlayerSpawnPoints[spawnIndex].position;
        }

        // Inspector 미연결 시 이름으로 자동 탐색
        var spawnObj = GameObject.Find($"StartPosition{_index + 1}");
        if (spawnObj != null)
            return spawnObj.transform.position;

        return new Vector3(_index * 2f, 0f, -85f);
    }

    // 첫 플레이어(호스트)를 기준점으로, 나머지 플레이어는 그 주위로 일정 간격 흩어 배치한다.
    // 인덱스별 절대 스폰 포인트(바위·지형과 겹칠 수 있는)에 의존하지 않아 안전하다.
    private Vector3 GetSpawnOffset(Vector3 _base, int _index)
    {
        if (_index == 0)
            return _base;

        // 호스트 주위 원형으로 배치 (1→오른쪽, 2→왼쪽, 3→앞 ... )
        float angle = (_index - 1) * (Mathf.PI * 0.5f); // 90도씩
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SPAWN_OFFSET_DISTANCE;
        return _base + offset;
    }

    // 스폰 포인트가 지형/바위와 겹치거나 y가 어긋나 있어도 항상 유효한 땅 위로 보정한다.
    // (스폰 포인트가 y=0 고정이라 지형 높이와 안 맞으면 캐릭터가 지형에 박히는 문제 방지)
    private Vector3 GetValidSpawnPosition(Vector3 _desired)
    {
        if (NavMesh.SamplePosition(_desired, out NavMeshHit hit, SPAWN_SAMPLE_RADIUS, NavMesh.AllAreas))
            return hit.position;

        return _desired;
    }
}
