#if UNITY_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Multiplayer.Playmode;
using UnityEngine;

// MPPM(Multiplayer Play Mode) QA 자동 부트스트랩.
// Ingame 씬 진입 시 CurrentPlayer.IsMainEditor로 역할을 분기해
//   메인 에디터 = Host, 가상 플레이어(클론) = Client 로 Relay 없이 로컬 직결 자동 접속한다.
// 사용자 개입 0(per-run): 가상 플레이어 최초 1회 활성만 수동(디스크 영속), 이후 Play 1회로 전 자동.
// 에디터 전용(MPPM은 에디터에서만 동작) — 전체 #if UNITY_EDITOR 가드.
public class MppmBootstrapWorker : MonoBehaviour
{
    public enum BootstrapState
    {
        Idle,
        DetectingRole,
        StartingHost,
        HostReady,
        WaitingForHost,
        Joining,
        ClientReady,
        Failed
    }

    [SerializeField] private bool m_AutoRunOnStart = true;
    [SerializeField] private float m_JoinTimeoutSeconds = 15f;
    [SerializeField] private float m_HostReadyPollSeconds = 0.25f;

    private BootstrapState m_State = BootstrapState.Idle;

    public BootstrapState State => m_State;

    private void Start()
    {
        if (m_AutoRunOnStart)
        {
            Run(destroyCancellationToken).Forget();
        }
    }

    public async UniTask Run(CancellationToken _ct = default)
    {
        if (m_State != BootstrapState.Idle && m_State != BootstrapState.Failed)
        {
            return;
        }

        m_State = BootstrapState.DetectingRole;

        // CurrentPlayer.IsMainEditor: 메인 에디터=true, 가상 플레이어(클론)=false.
        bool isMain = CurrentPlayer.IsMainEditor;

        try
        {
            // 스크립트 실행 순서 레이스 방지: GameNetworkManager가 NetworkManager/Transport를
            // 설정(SetupNetworkManager)하기 전에 StartHost/Client를 호출하면 NRE가 난다.
            // Managers.Network 생성 + 네트워크 설정 완료까지 대기한 뒤 진행한다.
            if (!await WaitUntilNetworkReadyAsync(_ct))
            {
                m_State = BootstrapState.Failed;
                GameDebug.LogError("[MppmBootstrap] Network layer not ready (timeout).");
                return;
            }

            if (isMain)
            {
                await RunAsHostAsync(_ct);
            }
            else
            {
                await RunAsClientAsync(_ct);
            }
        }
        catch (Exception e)
        {
            m_State = BootstrapState.Failed;
            GameDebug.LogError($"[MppmBootstrap] Bootstrap failed (isMain={isMain}): {e.Message}");
        }
    }

    // GameNetworkManager(Managers.Network) 생성 + NetworkManager/Transport 설정 완료까지 대기.
    private async UniTask<bool> WaitUntilNetworkReadyAsync(CancellationToken _ct)
    {
        float waited = 0f;
        const float STEP = 0.1f;

        while (waited < m_JoinTimeoutSeconds)
        {
            _ct.ThrowIfCancellationRequested();

            if (Managers.Network != null && Managers.Network.IsNetworkConfigured)
            {
                return true;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(STEP), cancellationToken: _ct);
            waited += STEP;
        }

        return false;
    }

    private async UniTask RunAsHostAsync(CancellationToken _ct)
    {
        m_State = BootstrapState.StartingHost;
        GameDebug.Log("[MppmBootstrap] Role=Host. Starting local host...");

        bool ok = await Managers.Network.StartLocalHostAsync(_ct);
        if (!ok)
        {
            m_State = BootstrapState.Failed;
            GameDebug.LogError("[MppmBootstrap] StartLocalHost failed.");
            return;
        }

        // 서버는 BuildingInfo 등 CSV 데이터가 있어야 스폰이 가능하다. 진입 경로와 무관하게 1회 보장.
        GameDebug.Log("[MppmBootstrap] Loading info data (server)...");
        await Managers.Info.LoadAllInfoOnce();

        m_State = BootstrapState.HostReady;
        GameDebug.Log("[MppmBootstrap] HostReady.");
    }

    private async UniTask RunAsClientAsync(CancellationToken _ct)
    {
        // 클론은 호스트가 listen을 시작하기 전 접속하면 실패하므로, 직결 시도를 타임아웃까지 재시도한다.
        m_State = BootstrapState.WaitingForHost;
        GameDebug.Log("[MppmBootstrap] Role=Clone. Waiting/joining local host...");

        float elapsed = 0f;
        while (elapsed < m_JoinTimeoutSeconds)
        {
            _ct.ThrowIfCancellationRequested();

            m_State = BootstrapState.Joining;
            bool started = await Managers.Network.StartLocalClientAsync(_ct);

            if (started && await WaitUntilConnectedAsync(_ct))
            {
                // 클론도 호스트와 동일하게 CSV(Info) 데이터를 로드해야 한다.
                // WorldDropItem.Info 등은 클라가 ItemInfoList를 직접 조회하므로,
                // 미로드 시 클론에서 item.Info == null이 된다(host 경로와 동일 멱등 호출).
                GameDebug.Log("[MppmBootstrap] Loading info data (client)...");
                await Managers.Info.LoadAllInfoOnce();

                m_State = BootstrapState.ClientReady;
                GameDebug.Log("[MppmBootstrap] ClientReady.");
                return;
            }

            // 실패 시 다음 시도 전 셧다운하고 잠깐 대기.
            if (Managers.Network.IsClient && !Managers.Network.IsRunning)
            {
                // 연결 거부 등으로 떨어진 경우 — StartLocalClient 가드가 IsListening을 보므로 정리 필요.
            }

            await UniTask.Delay(TimeSpan.FromSeconds(m_HostReadyPollSeconds), cancellationToken: _ct);
            elapsed += m_HostReadyPollSeconds;
        }

        m_State = BootstrapState.Failed;
        GameDebug.LogError($"[MppmBootstrap] Client join timed out after {m_JoinTimeoutSeconds}s.");
    }

    private async UniTask<bool> WaitUntilConnectedAsync(CancellationToken _ct)
    {
        float waited = 0f;
        const float STEP = 0.1f;
        const float CONNECT_WINDOW = 3f;

        while (waited < CONNECT_WINDOW)
        {
            _ct.ThrowIfCancellationRequested();

            if (Managers.Network.IsClient && Managers.Network.IsRunning)
            {
                return true;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(STEP), cancellationToken: _ct);
            waited += STEP;
        }

        return false;
    }
}
#endif
