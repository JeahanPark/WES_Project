---
name: MppmBootstrapWorker
category: Worker
parent: null
file_path: WES/Assets/Scripts/Worker/MppmBootstrapWorker.cs
role: "MPPM(Multiplayer Play Mode) QA 자동 부트스트랩. Ingame 씬 진입 시 CurrentPlayer.IsMainEditor로 역할을 분기해 메인 에디터=Host, 가상 플레이어(클론)=Client로 Relay 없이 로컬 직결(127.0.0.1) 자동 접속한다. BootstrapState 상태머신(Idle→DetectingRole→StartingHost/Joining→HostReady/ClientReady, 실패 시 Failed)으로 진행한다. 스크립트 실행 순서 레이스를 막기 위해 GameNetworkManager(Managers.Network)가 NetworkManager/Transport 설정을 끝내(IsNetworkConfigured) 준비될 때까지 대기한 뒤 StartLocalHost/Client를 호출한다. 클론은 호스트 listen 시작 전 접속하면 실패하므로 타임아웃까지 재시도한다. 사용자 개입 0(per-run) — 가상 플레이어 최초 1회 활성만 수동, 이후 Play 1회로 전 자동. 에디터 전용(#if UNITY_EDITOR)."
status: Active
signals: []
---

# MppmBootstrapWorker

MPPM(Multiplayer Play Mode) QA 자동 부트스트랩. Ingame 씬 진입 시 `CurrentPlayer.IsMainEditor`로 역할을 분기해 메인 에디터=Host, 가상 플레이어(클론)=Client로 Relay 없이 로컬 직결(127.0.0.1) 자동 접속한다.

## 동작

- **상태머신** `BootstrapState`: `Idle → DetectingRole → (StartingHost → HostReady | WaitingForHost → Joining → ClientReady)`, 실패 시 `Failed`.
- **네트워크 준비 대기**: `WaitUntilNetworkReadyAsync`가 `Managers.Network != null && IsNetworkConfigured`를 폴링한다. GameNetworkManager의 `SetupNetworkManager`가 끝나기 전에 StartHost/Client를 부르면 NRE가 나므로 레이스를 차단한다.
- **호스트**: [[GameNetworkManager]]`.StartLocalHostAsync` 호출.
- **클론**: [[GameNetworkManager]]`.StartLocalClientAsync`를 호스트 listen 시작 전 접속 실패에 대비해 `m_JoinTimeoutSeconds`까지 재시도. 접속 후 `WaitUntilConnectedAsync`로 `IsClient && IsRunning` 확인.
- 사용자 개입 0(per-run). 가상 플레이어 최초 1회 활성만 수동(디스크 영속), 이후 Play 1회로 전 자동.

## 관련

- 부모: (없음)
- 협력: [[GameNetworkManager]] (로컬 직결 Host/Client API), [[MultiplayerQaProbe]] (양측 상태 수집)
