---
name: GameNetworkManager
category: Manager
parent: "[[MonoSingleton]]"
file_path: WES/Assets/Scripts/Manager/GameNetworkManager.cs
role: "NGO(Netcode for GameObjects) 네트워크 매니저. NetworkManager/UnityTransport를 RequireComponent로 부착하고 Start의 SetupNetworkManager에서 NetworkConfig를 구성한다(런타임에 Resources의 DefaultNetworkPrefabs를 NetworkPrefabsList로 등록해 호스트/클라 프리팹 해시 일치 보장, transport 127.0.0.1:7777, TickRate 30 등). 설정 완료 시 IsNetworkConfigured=true로 외부(MPPM 부트스트랩)에 준비 신호를 준다. UnityServices/Auth 익명 로그인 초기화는 EnsureInitInternalAsync(IsInitialized)로 별도 관리한다. 접속 경로 2종: Relay(HostRelayAsync/JoinRelayAsync — Auth·JoinCode 필요)와 QA 멀티(MPPM) 전용 로컬 직결(StartLocalHostAsync/StartLocalClientAsync — 동일 머신이라 Relay 불필요, 127.0.0.1 직결, Auth 초기화 미대기). 플레이어 입·퇴장은 UniRx Subject(OnPlayerJoined/Left)로 발행한다."
status: Active
signals: []
---

# GameNetworkManager

NGO 네트워크 매니저. `NetworkManager`/`UnityTransport`를 `RequireComponent`로 부착하고, `Start`의 `SetupNetworkManager`에서 `NetworkConfig`를 구성한다.

## 핵심

- **NetworkConfig 구성**: 동적 생성 NetworkManager는 NetworkConfig가 비어 NetworkPrefabsList가 빠진다 → 런타임에 `Resources`의 `DefaultNetworkPrefabs`를 `RegisterNetworkPrefabs`로 등록해 호스트/클라 프리팹 해시를 일치시킨다(없으면 클라가 서버 스폰 오브젝트를 복원 못 함). transport `127.0.0.1:7777`, TickRate 30, EnableSceneManagement 등 설정 후 **`IsNetworkConfigured = true`**.
- **`IsNetworkConfigured` 플래그(신규)**: 외부([[MppmBootstrapWorker]])가 StartHost/Client 전 설정 완료를 폴링하는 레이스 방지용 신호.
- **온라인 초기화 분리**: `EnsureInitInternalAsync`가 UnityServices Init + 익명 Auth 로그인 → `IsInitialized`. Relay 경로만 이걸 기다린다.

## 접속 경로

| 경로 | 메서드 | 특징 |
|------|--------|------|
| Relay | `HostRelayAsync` / `JoinRelayAsync` | Auth·JoinCode 필요, `WaitUntilInitializedAsync` 대기 |
| 로컬 직결(MPPM QA, 신규) | `StartLocalHostAsync` / `StartLocalClientAsync` | 동일 머신 — Relay 불필요, 127.0.0.1 직결, Auth 초기화 미대기(Init 대기 시 인증 미완료로 StartHost가 막힘), 이미 IsListening이면 skip |

플레이어 입·퇴장은 UniRx `OnPlayerJoinedAsObservable` / `OnPlayerLeftAsObservable`로 발행.

## 관련

- 부모: [[MonoSingleton]]
- 협력: [[MppmBootstrapWorker]] (로컬 직결 자동 접속)
