using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 게임 진행을 관리하는 Worker
/// - 플레이어/몬스터 스폰
/// </summary>
public class InGamePlayWorker : NetworkBehaviour
{
    private const string PLAYER_PREFAB_KEY = "PlayerCharacter";
    private const string TEST_MONSTER_PREFAB_KEY = "Test01Monster";

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
        SpawnTestMonster();
    }

    /// <summary>
    /// 로컬 플레이어 등록 (PlayerCharacter에서 호출)
    /// </summary>
    public void RegisterLocalPlayer(PlayerCharacter _player)
    {
        m_LocalPlayer = _player;

        GameDebug.Log($"[InGamePlayWorker] Local player registered: PlayerIndex {_player.GetPlayerIndex()}");
    }

    public void SpawnMonster(string _prefabKey, Vector3 _position)
    {
        if (!Managers.Network.IsServer)
            return;

        GameObject prefab = Managers.Resource.LoadAddressable<GameObject>(_prefabKey);
        if (prefab == null)
        {
            GameDebug.LogError($"[InGamePlayWorker] Monster prefab not found: {_prefabKey}");
            return;
        }

        GameObject instance = Instantiate(prefab, _position, Quaternion.identity);
        instance.SetActive(true);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
            GameDebug.Log($"[InGamePlayWorker] Monster spawned: {_prefabKey} at {_position}");
        }
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

        foreach (ulong clientId in clientIds)
        {
            Vector3 spawnPosition = GetPlayerSpawnPosition(playerIndex);

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

    private void SpawnTestMonster()
    {
        if (!Managers.Network.IsServer)
            return;

        SpawnMonster(TEST_MONSTER_PREFAB_KEY, Vector3.zero);
    }

    private Vector3 GetPlayerSpawnPosition(int _index)
    {
        if (m_PlayerSpawnPoints != null && m_PlayerSpawnPoints.Length > 0)
        {
            int spawnIndex = _index % m_PlayerSpawnPoints.Length;
            return m_PlayerSpawnPoints[spawnIndex].position;
        }

        return Vector3.zero + new Vector3(_index * 2f, 0f, 0f);
    }
}
