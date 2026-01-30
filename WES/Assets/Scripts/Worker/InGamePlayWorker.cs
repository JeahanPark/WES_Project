using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 게임 진행을 관리하는 Worker
/// - 플레이어 스폰
/// </summary>
public class InGamePlayWorker : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerCharacter m_PlayerPrefab;
    [SerializeField] private Transform[] m_SpawnPoints;

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

    private void SpawnAllPlayers()
    {
        if (!Managers.Network.IsServer)
            return;

        if (m_PlayerPrefab == null)
        {
            GameDebug.LogError("[InGamePlayWorker] Player prefab is not assigned!");
            return;
        }

        var objectDataWorker = InGameController.Instance.ObjectDataWorker;
        if (objectDataWorker == null)
        {
            GameDebug.LogError("[InGamePlayWorker] ObjectDataWorker is not assigned!");
            return;
        }

        ulong[] clientIds = Managers.Network.GetConnectedClientIds();
        int playerIndex = 0;

        foreach (ulong clientId in clientIds)
        {
            Vector3 spawnPosition = GetSpawnPosition(playerIndex);

            PlayerCharacter player = Instantiate(m_PlayerPrefab, spawnPosition, Quaternion.identity);
            player.gameObject.SetActive(true);

            NetworkObject networkObject = player.GetComponent<NetworkObject>();

            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                player.SetPlayerIndex(playerIndex);

                // ObjectDataWorker에 등록
                objectDataWorker.RegisterPlayer(player);

                GameDebug.Log($"[InGamePlayWorker] Spawned player {playerIndex} for client {clientId}");
            }

            playerIndex++;
        }

        GameDebug.Log($"[InGamePlayWorker] All players spawned! Total: {playerIndex}");
    }

    private Vector3 GetSpawnPosition(int _playerIndex)
    {
        if (m_SpawnPoints != null && m_SpawnPoints.Length > 0)
        {
            int spawnIndex = _playerIndex % m_SpawnPoints.Length;
            return m_SpawnPoints[spawnIndex].position;
        }

        return Vector3.zero + new Vector3(_playerIndex * 2f, 0f, 0f);
    }
}
