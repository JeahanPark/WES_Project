using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class InGameController : GameController<InGameController>
{
    [Header("Components")]
    [SerializeField] private Canvas m_Canvas;
    [SerializeField] private InGameCameraWorker m_CameraWorker;

    [Header("Player Prefab")]
    [SerializeField] private PlayerCharacter m_PlayerPrefab;

    [Header("Test Mode")]
    [SerializeField] private bool m_TestMode = false;

    // Network
    private NetworkObject m_NetworkObject;
    private HashSet<ulong> m_ReadyClients = new();
    private bool m_IsGameStarted = false;

    // Local Player
    private PlayerCharacter m_LocalPlayer;

    public InGameCameraWorker CameraWorker => m_CameraWorker;
    public bool IsGameStarted => m_IsGameStarted;

    public void RegisterLocalPlayer(PlayerCharacter _player)
    {
        m_LocalPlayer = _player;
    }

    private IEnumerator Start()
    {
        m_NetworkObject = GetComponent<NetworkObject>();

        if (m_TestMode)
        {
            yield return CoInitializeTestMode();
        }
        // 게임 시작 대기
        yield return CoWaitForGameStart();
    }

    private IEnumerator CoWaitForGameStart()
    {
        Managers.Popup.InitializeForScene(m_Canvas);

        Debug.Log("[InGameController] Waiting for game start...");

        // NetworkObject 스폰 대기
        yield return new WaitUntil(() => m_NetworkObject != null && m_NetworkObject.IsSpawned);

        // 서버에 준비 완료 알림
        NotifyClientReadyServerRpc(Managers.Network.GetLocalClientId());

        // 게임 시작 신호 대기
        yield return new WaitUntil(() => m_IsGameStarted);

        Debug.Log("[InGameController] Game start signal received!");

        // 로컬 플레이어 스폰 대기
        yield return CoWaitForLocalPlayer();

        Debug.Log("[InGameController] Game started! Local player is ready.");
    }

    private IEnumerator CoWaitForLocalPlayer()
    {
        Debug.Log("[InGameController] Waiting for local player spawn...");

        // 로컬 플레이어가 스폰될 때까지 대기
        yield return new WaitUntil(() => m_LocalPlayer != null);

        Debug.Log($"[InGameController] Local player spawned: {m_LocalPlayer.GetPlayerIndex()}");
    }

    private IEnumerator CoInitializeTestMode()
    {
        Debug.Log("[InGameController] TestMode enabled - Initializing all systems...");

        // 1. Managers 초기화 (Intro 씬에서 하는 것과 동일)
        Managers.Instance.Init();
        yield return null;

        // 3. Network 초기화 대기
        Debug.Log("[InGameController] Waiting for Network initialization...");
        yield return new WaitUntil(() => Managers.Network.IsInitialized);

        // 4. 테스트용 Relay Host 시작
        Debug.Log("[InGameController] Starting test Relay host...");
        var hostTask = Managers.Network.HostRelayAsync(destroyCancellationToken);
        yield return new WaitUntil(() => hostTask.Status != Cysharp.Threading.Tasks.UniTaskStatus.Pending);

        if (hostTask.Status == Cysharp.Threading.Tasks.UniTaskStatus.Succeeded)
        {
            string roomCode = hostTask.GetAwaiter().GetResult();
            Debug.Log($"[InGameController] TestMode room created with code: {roomCode}");
        }
        else
        {
            Debug.LogError("[InGameController] TestMode failed to create room");
        }

        Debug.Log("[InGameController] TestMode initialization complete!");
    }

    [Rpc(SendTo.Server)]
    private void NotifyClientReadyServerRpc(ulong _clientId)
    {
        if (!Managers.Network.IsServer)
            return;

        m_ReadyClients.Add(_clientId);
        int connectedCount = Managers.Network.GetConnectedPlayerCount();
        Debug.Log($"[InGameController] Client {_clientId} is ready. ({m_ReadyClients.Count}/{connectedCount})");

        // 모든 클라이언트가 준비되었는지 확인
        if (m_ReadyClients.Count >= connectedCount)
        {
            Debug.Log("[InGameController] All clients ready! Spawning players...");
            SpawnAllPlayers();
            StartGameClientRpc();
        }
    }

    private void SpawnAllPlayers()
    {
        if (!Managers.Network.IsServer)
            return;

        if (m_PlayerPrefab == null)
        {
            Debug.LogError("[InGameController] Player prefab is not assigned!");
            return;
        }

        ulong[] clientIds = Managers.Network.GetConnectedClientIds();
        int playerIndex = 0;

        foreach (ulong clientId in clientIds)
        {
            // 플레이어 스폰 위치 계산 (TODO: 실제 스폰 포인트 사용)
            Vector3 spawnPosition = Vector3.zero + new Vector3(playerIndex * 2f, 0f, 0f);

            // 플레이어 생성
            PlayerCharacter player = Instantiate(m_PlayerPrefab, spawnPosition, Quaternion.identity);
            player.gameObject.SetActive(true); // 프리팹이 비활성화 상태이므로 활성화

            NetworkObject networkObject = player.GetComponent<NetworkObject>();

            if (networkObject != null)
            {
                // 해당 클라이언트의 소유로 스폰
                networkObject.SpawnAsPlayerObject(clientId);
                player.SetPlayerIndex(playerIndex);

                Debug.Log($"[InGameController] Spawned player {playerIndex} for client {clientId}");
            }

            playerIndex++;
        }

        Debug.Log($"[InGameController] All players spawned! Total: {playerIndex}");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartGameClientRpc()
    {
        m_IsGameStarted = true;
        Debug.Log("[InGameController] Received game start signal!");
    }
}
