using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Cysharp.Threading.Tasks;
using UniRx;

public class InGameController : NetworkGameController<InGameController>
{
#if UNITY_EDITOR
    // QA 풀플레이(E2E): Intro→Ingame 점프 시 씬 로드 전에 켜두면
    // Ingame InGameController가 Start에서 기존 CoInitializeTestMode(자동 Host 셋업)를 그대로 탄다.
    private static bool s_ForceTestModeOnLoad = false;
#endif

    [SerializeField] private Canvas m_Canvas;
    [Header("Worker")]
    [SerializeField] private InGameCameraWorker m_CameraWorker;
    [SerializeField] private InGamePlayWorker m_PlayWorker;
    [SerializeField] private InGameHUDWorker m_HUDWorker;
    [SerializeField] private InGameWorldUIWorker m_WorldUIWorker;
    [SerializeField] private InGameObjectDataWorker m_ObjectDataWorker;
    [SerializeField] private InGameColliderWorker m_ColliderWorker;
    [SerializeField] private InGameSpawnWorker m_SpawnWorker;
    [SerializeField] private InGameAreaWorker m_AreaWorker;
    [SerializeField] private BuildingPlacementWorker m_BuildingPlacementWorker;
    [SerializeField] private DayNightWorker m_DayNightWorker;
    [SerializeField] private DayNightRenderWorker m_DayNightRenderWorker;
    [SerializeField] private ColdDamageWorker m_ColdDamageWorker;

    [Header("Test Mode")]
    [SerializeField] private bool m_TestMode = false;

    // Network
    // 자체 NetworkObject 필드를 두지 않는다. NetworkBehaviour 베이스가 제공하는
    // NetworkObject 프로퍼티와 동일 이름으로 직렬화 충돌이 나기 때문(NGO 직렬화 규칙 위반).
    // 베이스의 NetworkObject / IsSpawned 를 직접 사용한다.
    private HashSet<ulong> m_ReadyClients = new();
    private bool m_IsGameStarted = false;

    // Game State
    private GameState m_GameState = GameState.Playing;
    private int m_EscapedCount = 0;
    private int m_AlivePlayerCount = 0;

    public GameState GameState => m_GameState;

    public InGameCameraWorker CameraWorker => m_CameraWorker;
    public InGamePlayWorker PlayWorker => m_PlayWorker;
    public InGameHUDWorker HUDWorker => m_HUDWorker;
    public InGameWorldUIWorker WorldUIWorker => m_WorldUIWorker;
    public InGameObjectDataWorker ObjectDataWorker => m_ObjectDataWorker;
    public InGameColliderWorker ColliderWorker => m_ColliderWorker;
    public InGameSpawnWorker SpawnWorker => m_SpawnWorker;
    public InGameAreaWorker AreaWorker => m_AreaWorker;
    public BuildingPlacementWorker BuildingPlacementWorker => m_BuildingPlacementWorker;
    public DayNightWorker DayNightWorker => m_DayNightWorker;
    public DayNightRenderWorker DayNightRenderWorker => m_DayNightRenderWorker;
    public ColdDamageWorker ColdDamageWorker => m_ColdDamageWorker;
    public bool IsGameStarted => m_IsGameStarted;

    private IEnumerator Start()
    {
        bool testMode = m_TestMode;
#if UNITY_EDITOR
        testMode = testMode || ConsumeTestModeRequest();
#endif
        if (testMode)
        {
            yield return CoInitializeTestMode();
        }
        // 게임 시작 대기
        yield return CoWaitForGameStart();
    }

    private IEnumerator CoWaitForGameStart()
    {
        Managers.Popup.InitializeForScene(m_Canvas);
        SubscribeUIInput();
        m_WorldUIWorker.Initialize(m_CameraWorker.GetCamera());

        GameDebug.Log("[InGameController] Waiting for game start...");

        // NetworkObject 스폰 대기 (베이스 NetworkBehaviour.IsSpawned 사용)
        yield return new WaitUntil(() => IsSpawned);

        // 서버에 준비 완료 알림
        NotifyClientReadyServerRpc(Managers.Network.GetLocalClientId());

        // 게임 시작 신호 대기
        yield return new WaitUntil(() => m_IsGameStarted);

        GameDebug.Log("[InGameController] Game start signal received!");

        // 로컬 플레이어 스폰 대기
        yield return CoWaitForLocalPlayer();

        GameDebug.Log("[InGameController] Game started! Local player is ready.");

#if UNITY_EDITOR
        TestManager.Instance?.FillStartInventory(m_ObjectDataWorker);
#endif
    }

#if UNITY_EDITOR
    public static void RequestTestModeOnLoad() => s_ForceTestModeOnLoad = true;

    private static bool ConsumeTestModeRequest()
    {
        bool requested = s_ForceTestModeOnLoad;
        s_ForceTestModeOnLoad = false;
        return requested;
    }

    public void TestSpawnCampfireNearPlayer()
    {
        TestManager.Instance?.TestSpawnCampfireNearPlayer();
    }

    public void TestPopupEscapeAndUIGuard()
    {
        TestManager.Instance?.TestPopupEscapeAndUIGuard();
    }
    public void TestMonsterRespawnDamage()
    {
        TestManager.Instance?.TestMonsterRespawnDamage();
    }

    public void TestPlayerDeathAndGameOver()
    {
        TestManager.Instance?.TestPlayerDeathAndGameOver();
    }

    public void TestDayNightCycle()
    {
        TestManager.Instance?.TestDayNightCycle();
    }
#endif

    private void SubscribeUIInput()
    {
        if (Managers.Input == null)
            return;

        Managers.Input.OnCancelAsObservable
            .Subscribe(_ => Managers.Popup?.CloseTop())
            .AddTo(this);
    }

    private IEnumerator CoWaitForLocalPlayer()
    {
        GameDebug.Log("[InGameController] Waiting for local player spawn...");

        // 로컬 플레이어가 스폰될 때까지 대기
        yield return new WaitUntil(() => m_PlayWorker.LocalPlayer != null);

        GameDebug.Log($"[InGameController] Local player spawned: {m_PlayWorker.LocalPlayer.GetPlayerIndex()}");
    }

    private IEnumerator CoInitializeTestMode()
    {
        GameDebug.Log("[InGameController] TestMode enabled - Initializing all systems...");

        // 1. Managers 초기화 (Intro 씬에서 하는 것과 동일)
        Managers.Instance.Init();
        yield return null;

        // 2. Info 로드
        GameDebug.Log("[InGameController] Loading info data...");
        yield return Managers.Info.LoadAllInfoOnce().ToCoroutine();
        GameDebug.Log("[InGameController] Info data loaded!");

        // 3. Network 초기화 대기
        GameDebug.Log("[InGameController] Waiting for Network initialization...");
        yield return new WaitUntil(() => Managers.Network.IsInitialized);

        // 4. 테스트용 Relay Host 시작
        GameDebug.Log("[InGameController] Starting test Relay host...");
        var hostTask = Managers.Network.HostRelayAsync(destroyCancellationToken);
        yield return new WaitUntil(() => hostTask.Status != Cysharp.Threading.Tasks.UniTaskStatus.Pending);

        if (hostTask.Status == Cysharp.Threading.Tasks.UniTaskStatus.Succeeded)
        {
            string roomCode = hostTask.GetAwaiter().GetResult();
            GameDebug.Log($"[InGameController] TestMode room created with code: {roomCode}");
        }
        else
        {
            GameDebug.LogError("[InGameController] TestMode failed to create room");
        }

        GameDebug.Log("[InGameController] TestMode initialization complete!");
    }

    [Rpc(SendTo.Server)]
    private void NotifyClientReadyServerRpc(ulong _clientId)
    {
        if (!Managers.Network.IsServer)
            return;

        m_ReadyClients.Add(_clientId);
        int connectedCount = Managers.Network.GetConnectedPlayerCount();
        GameDebug.Log($"[InGameController] Client {_clientId} is ready. ({m_ReadyClients.Count}/{connectedCount})");

        // 모든 클라이언트가 준비되었는지 확인
        if (m_ReadyClients.Count >= connectedCount)
        {
            GameDebug.Log("[InGameController] All clients ready! Starting game...");
            m_AlivePlayerCount = connectedCount;
            m_PlayWorker.StartGame();
            m_AreaWorker.Initialize();
            StartGameClientRpc();
        }
    }

    [Rpc(SendTo.Server)]
    public void NotifyPlayerDiedServerRpc()
    {
        if (m_GameState != GameState.Playing)
            return;

        m_AlivePlayerCount--;
        GameDebug.Log($"[InGameController] Player died. Alive: {m_AlivePlayerCount}");

        if (m_AlivePlayerCount <= 0)
        {
            TriggerGameOver();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartGameClientRpc()
    {
        m_IsGameStarted = true;
        GameDebug.Log("[InGameController] Received game start signal!");
    }

    /// <summary>
    /// EscapePoint에서 플레이어가 도달했을 때 호출 (클라이언트 - 로컬 오너만)
    /// </summary>
    public void OnPlayerReachedEscape(PlayerCharacter _player)
    {
        if (m_GameState != GameState.Playing)
            return;

        if (_player.IsDead)
            return;

        GameDebug.Log($"[InGameController] Player {_player.GetPlayerIndex()} reached escape point!");
        NotifyEscapeServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void NotifyEscapeServerRpc()
    {
        if (m_GameState != GameState.Playing)
            return;

        m_EscapedCount++;
        int totalPlayers = Managers.Network.GetConnectedPlayerCount();
        GameDebug.Log($"[InGameController] Escaped: {m_EscapedCount}/{totalPlayers}");

        if (m_EscapedCount >= totalPlayers)
        {
            TriggerClearClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerClearClientRpc()
    {
        m_GameState = GameState.Clear;
        GameDebug.Log("[InGameController] GAME CLEAR!");
        ShowResultPopup(GameState.Clear);
    }

    public void TriggerGameOver()
    {
        if (m_GameState != GameState.Playing)
            return;

        TriggerGameOverClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerGameOverClientRpc()
    {
        m_GameState = GameState.GameOver;
        GameDebug.Log("[InGameController] GAME OVER!");

        // 전멸 연출(director 결정 2026-06-05): death_overlay 1.8초 암전 → 완료 후
        // ResultPopup 표시(BgDefeat가 어둠에서 떠오름). 오버레이가 없으면 즉시 표시.
        var overlay = m_HUDWorker != null ? m_HUDWorker.CoreTensionOverlay : null;
        if (overlay != null)
            overlay.PlayDeathFade(() => ShowResultPopup(GameState.GameOver));
        else
            ShowResultPopup(GameState.GameOver);
    }

    private void ShowResultPopup(GameState _state)
    {
        var popup = Managers.Popup.Open<ResultPopup>();
        if (popup != null)
            popup.ShowResult(_state);
    }
}
