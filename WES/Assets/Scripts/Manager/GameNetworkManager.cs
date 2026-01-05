using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UniRx;

public class GameNetworkManager : MonoSingleton<GameNetworkManager>
{
    public string GetCode => m_Code;
    public bool IsRunning => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    public bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    public bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
    public int ConnectedClientCount => NetworkManager.Singleton?.ConnectedClients?.Count ?? 0;

    public bool IsInitialized => m_IsInitialized;

    public IObservable<Unit> OnPlayerJoinedAsObservable => m_OnPlayerJoined;
    public IObservable<Unit> OnPlayerLeftAsObservable => m_OnPlayerLeft;

    private readonly Subject<Unit> m_OnPlayerJoined = new Subject<Unit>();
    private readonly Subject<Unit> m_OnPlayerLeft = new Subject<Unit>();

    private string m_Code;
    private bool m_IsInitialized;
    private bool m_IsInitializing;
    private UniTaskCompletionSource m_InitTcs;

    public override void Init()
    {
        base.Init();
        EnsureInitInternalAsync().Forget();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void Clear()
    {
        base.Clear();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("NetworkManager Shutdown");
            }
        }

        m_Code = null;

        if (AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
            Debug.Log("Signed out from Authentication Service");
        }

        m_OnPlayerJoined?.Dispose();
        m_OnPlayerLeft?.Dispose();

        m_IsInitialized = false;
        m_IsInitializing = false;
        m_InitTcs = null;
    }

    public int GetConnectedPlayerCount()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClients == null)
            return 0;

        return NetworkManager.Singleton.ConnectedClients.Count;
    }

    public ulong GetLocalClientId()
    {
        if (NetworkManager.Singleton == null)
            return 0;

        return NetworkManager.Singleton.LocalClientId;
    }

    public ulong[] GetConnectedClientIds()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClients == null)
            return Array.Empty<ulong>();

        var ids = new ulong[NetworkManager.Singleton.ConnectedClients.Count];
        int index = 0;
        foreach (var clientId in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            ids[index++] = clientId;
        }
        Array.Sort(ids);
        return ids;
    }

    public UniTask WaitUntilInitializedAsync(CancellationToken _ct = default)
    {
        if (m_IsInitialized) return UniTask.CompletedTask;
        m_InitTcs ??= new UniTaskCompletionSource();
        return m_InitTcs.Task.AttachExternalCancellation(_ct);
    }

    public async UniTask<string> HostRelayAsync(CancellationToken _ct)
    {
        await WaitUntilInitializedAsync(_ct);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[GameNetworkManager] NetworkManager.Singleton is null. Please add NetworkManager to the scene.");
            return null;
        }

        Allocation alloc = await RelayService.Instance
            .CreateAllocationAsync(2)
            .AsUniTask();

        string code = await RelayService.Instance
            .GetJoinCodeAsync(alloc.AllocationId)
            .AsUniTask();

        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.SetHostRelayData(
            alloc.RelayServer.IpV4,
            (ushort)alloc.RelayServer.Port,
            alloc.AllocationIdBytes,
            alloc.Key,
            alloc.ConnectionData
        );

        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.StartHost();

        m_Code = code;
        Debug.Log($"Relay Host started. Code={code}");
        return code;
    }

    public async UniTask<bool> JoinRelayAsync(string _code, CancellationToken _ct)
    {
        if (string.IsNullOrWhiteSpace(_code))
        {
            Debug.LogWarning("Join code is empty.");
            return false;
        }

        await WaitUntilInitializedAsync(_ct);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[GameNetworkManager] NetworkManager.Singleton is null. Please add NetworkManager to the scene.");
            return false;
        }

        JoinAllocation join = await RelayService.Instance
            .JoinAllocationAsync(_code)
            .AsUniTask();

        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.SetClientRelayData(
            join.RelayServer.IpV4,
            (ushort)join.RelayServer.Port,
            join.AllocationIdBytes,
            join.Key,
            join.ConnectionData,
            join.HostConnectionData
        );

        bool ok = NetworkManager.Singleton.StartClient();
        Debug.Log("Relay Client connecting...");
        return ok;
    }

    private async UniTask EnsureInitInternalAsync(float _timeoutSeconds = 20f)
    {
        if (m_IsInitialized) return;

        if (m_IsInitializing)
        {
            await WaitUntilInitializedAsync().Timeout(TimeSpan.FromSeconds(_timeoutSeconds));
            return;
        }

        m_IsInitializing = true;
        m_InitTcs ??= new UniTaskCompletionSource();

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync()
                    .AsUniTask()
                    .Timeout(TimeSpan.FromSeconds(_timeoutSeconds));
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync()
                    .AsUniTask()
                    .Timeout(TimeSpan.FromSeconds(_timeoutSeconds));
            }

            m_IsInitialized = true;
            m_InitTcs.TrySetResult();
            Debug.Log("[GameNetworkManager] Services & Auth initialized");
        }
        catch (Exception e)
        {
            m_InitTcs.TrySetException(e);
            Debug.LogError($"[GameNetworkManager] EnsureInit failed: {e}");
            throw;
        }
        finally
        {
            m_IsInitializing = false;
        }
    }

    private void HandleServerStarted()
    {
        Debug.Log("[GameNetworkManager] Server started");

        if (NetworkManager.Singleton.IsHost)
        {
            ulong hostClientId = NetworkManager.Singleton.LocalClientId;
            Debug.Log($"[GameNetworkManager] Host registered as client: {hostClientId}");
        }
    }

    private void HandleClientConnected(ulong _clientId)
    {
        Debug.Log($"[GameNetworkManager] Client connected: {_clientId}");
        m_OnPlayerJoined.OnNext(Unit.Default);
    }

    private void HandleClientDisconnected(ulong _clientId)
    {
        Debug.Log($"[GameNetworkManager] Client disconnected: {_clientId}");
        m_OnPlayerLeft.OnNext(Unit.Default);
    }
}
