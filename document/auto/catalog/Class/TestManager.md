---
name: TestManager
category: Manager
parent: "[[MonoSingleton]]"
file_path: WES/Assets/Scripts/Manager/TestManager.cs
role: "QA/E2E 테스트 전용 매니저(#if UNITY_EDITOR, Managers.Test). 입력 시뮬레이션, 풀플레이 E2E, 시작 인벤토리 채우기, 낮밤·몬스터·게임오버 등 기존 public 메서드를 조합한 테스트 시나리오 트리거를 제공한다(테스트 전용 로직 금지 원칙). MPPM 멀티 동기화 검증용으로 TestMultiSpawnForSync(호스트 권위 몬스터 등 스폰)와 TestMpV2_MovePlayer/V4_DamageMonster/V5_KillMonster/V6_CollectDropItem/V7_ChangeCold 트리거를 추가했다 — 호스트는 QA의 u_play_invoke로, 클론은 MultiplayerQaProbe의 cmd_clone.json 화이트리스트(TestMp*) 경유로 호출된다. 서버 권위 메서드(V4/V5/V7)는 IsServer 가드로 호스트에서만, V6 수집은 ServerRpc 왕복 검증을 위해 클론 측에서 호출한다."
status: Active
signals: []
---

# TestManager

QA/E2E 테스트 전용 매니저. `#if UNITY_EDITOR` 전용 `MonoSingleton`(`Managers.Test`). 테스트 전용 로직을 두지 않고 기존 public 메서드를 조합만 한다.

## 기능

- **입력 시뮬레이션**: `SimulateKeyPress` 등.
- **풀플레이 E2E**: `FULLPLAY_*` 상수 기반 자동 플레이.
- **시작 인벤토리**: `FillStartInventory`.
- **단일 시나리오**: `TestDayNightCycle`, `TestMonsterRespawnDamage`, `TestPlayerDeathAndGameOver`, `TestSpawnCampfireNearPlayer` 등.

## MPPM 멀티 동기화 트리거(신규)

| 메서드 | 권위 | 호출 위치 | 내용 |
|--------|------|-----------|------|
| `TestMultiSpawnForSync` | 서버 | 호스트 | 호스트 권위 몬스터 등 스폰 → 양측 스냅샷 수집 준비(V3 전파·V4 HP·V5 사망 대상) |
| `TestMpV2_MovePlayer` | 오너 | 양측 | 자기 로컬 플레이어를 1초 이동(ClientNetworkTransform 전파) → 정지 수렴값 비교 |
| `TestMpV4_DamageMonster` | 서버 | 호스트 | 몬스터 HP 감소(0 제외, 사망은 V5 분리) |
| `TestMpV5_KillMonster` | 서버 | 호스트 | 몬스터 HP=0 → 디스폰 + 드롭 스폰 |
| `TestMpV6_CollectDropItem` | 오너 클라 | **클론** | `drop.CollectServerRpc()` 왕복 — 클론서 호출해야 왕복 검증 |
| `TestMpV7_ChangeCold` | 서버 | 호스트 | 플레이어 Cold 변화 |

- 호스트 트리거는 QA가 `u_play_invoke`로 직접 호출. 클론 트리거(V6)는 파이프가 막혀 있어 [[MultiplayerQaProbe]]의 `cmd_clone.json` 화이트리스트(`TestMp*` 무인자) 채널로 호출된다.
- 서버 권위(V4/V5/V7)는 `IsServer` 가드로 호스트에서만 실행.

## 관련

- 부모: [[MonoSingleton]]
- 협력: [[MultiplayerQaProbe]] (클론 커맨드 대상), [[MppmBootstrapWorker]] (멀티 환경 부트스트랩)
