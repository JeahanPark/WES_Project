---
작성일: 2026-05-05
주제: 플레이어 사망 처리 + 전멸 시 게임오버 트리거
상태: 승인됨
---

# 플레이어 사망 & 게임오버 — 설계 문서

## 1. 배경

GDD에 정의된 게임 종료 조건은 두 가지:
- 클리어: EscapePoint 도달 (이미 구현)
- 게임 오버: 전원 사망 (미구현)

`InGameController.TriggerGameOver()` 메서드는 존재하지만 **호출하는 곳이 0건**. 모든 플레이어가 죽어도 게임이 계속 진행됨.

## 2. 결정된 동작

- 플레이어 사망 시:
  1. 본인 입력 차단 (Move/Attack/Interact)
  2. Animator Death 모션 재생 후 정지 (시체)
  3. 본인 카메라가 자신을 따라가고 있다면 → 살아있는 동료로 전환
  4. 서버에 사망 보고
- 서버: 살아있는 플레이어 카운트 -1
  - 0명이면 → `TriggerGameOver()` 호출 (이미 구현된 RPC가 GameState=GameOver + 3초 후 로비 복귀)

혼자 플레이 중 사망하면: "살아있는 플레이어 == 0" 단일 규칙으로 자연스럽게 즉시 게임오버.

## 3. 비목표

- 리스폰 (영구 사망)
- 사망 후 자유 카메라 모드 (혼자 죽으면 즉시 종료)
- 게임오버 화면 UI (M3 Week 4 UI/UX 작업 범위)
- "개인 탈출" 처리 (별도 스펙)

## 4. 변경 범위 (5개 파일)

| 파일 | 변경 |
|------|------|
| `CharacterRegistry.cs` | `GetAlivePlayers()` 헬퍼 추가 — 살아있는 PlayerCharacter 리스트 반환 |
| `PlayerCharacter.cs` | • `OnDeath()` override<br>• 본인이면 입력/카메라 처리, 서버에 사망 보고<br>• `Attack()` / `Interact()` / `MoveWithDirection()` 시작부에 `if (IsDead) return;` 가드 |
| `CharacterBase.cs` | `MoveWithDirection()`에 `IsDead` 가드 추가 (PlayerCharacter도 공유) |
| `InGameController.cs` | • 살아있는 플레이어 카운트 추적 (`m_AlivePlayerCount`, 서버 권한)<br>• 게임 시작 시 카운트 초기화<br>• `NotifyPlayerDiedServerRpc()` 추가 → 카운트 감소 → 0이면 `TriggerGameOver()` |
| `InGameCameraWorker.cs` | 변경 없음 — 기존 `SetTarget(Transform)` 메서드 재사용 |

## 5. 데이터 흐름

```
플레이어 HP=0
  CharacterBase.TakeDamageServerRpc → SetHP(0) → IsDead=true
    OnDeathClientRpc (모든 클라이언트)
      → m_OnDeath?.Invoke()
      → OnDeath() virtual 호출
        PlayerCharacter.OnDeath() override
          ├─ 본인 (IsOwner) 처리:
          │   ├─ 입력 가드는 IsDead로 자동 (이미 NetworkVariable 동기화됨)
          │   ├─ 카메라 타겟이 본인이면 → CharacterRegistry.GetAlivePlayers()
          │   │     첫 살아있는 동료 → CameraWorker.SetTarget(그 사람.transform)
          │   └─ 서버에 보고: NotifyPlayerDiedServerRpc()
          │       (서버) m_AlivePlayerCount--
          │              if (count == 0) TriggerGameOver()
          └─ 모두: Animator Death 모션 (StateAnimationComponent? 플레이어는 PlayerAnimationComponent)
```

## 6. 입력 차단 방식

`CharacterBase.IsDead`는 NetworkVariable 기반 → 모든 클라이언트가 즉시 동기화. 한 줄 가드면 충분:

```csharp
public override void Attack()
{
    if (IsDead) return;
    if (TryCollectClickedDropItem()) return;
    ...
}
```

`MoveWithDirection`은 `CharacterBase`에 정의돼 있으므로 거기 한 곳에 가드 추가하면 PlayerCharacter/MonsterBase 모두 적용 (몬스터는 이미 OnDeath 시 SetCollisionEnabled(false)이지만 Move도 차단되는 게 안전).

## 7. 살아있는 동료 카메라 전환

```csharp
// PlayerCharacter.OnDeath() 중 IsOwner 분기
var registry = InGameController.Instance.ObjectDataWorker.GetCharacterRegistry();
PlayerCharacter aliveTeammate = null;
foreach (ulong id in registry.GetAllCharacterIds())
{
    var p = registry.GetPlayer(id);
    if (p == null || p == this) continue;
    if (p.IsDead) continue;
    aliveTeammate = p;
    break;
}
if (aliveTeammate != null)
{
    InGameController.Instance.CameraWorker.SetTarget(aliveTeammate.transform);
}
// aliveTeammate가 없는 경우 → 어차피 NotifyPlayerDiedServerRpc로 서버에서 게임오버 발화 → 카메라 그대로
```

깔끔하게 `CharacterRegistry.GetAlivePlayers()` 헬퍼로 추출.

## 8. 서버 카운트 관리

```csharp
// InGameController
private int m_AlivePlayerCount = 0;

// 게임 시작 시 (NotifyClientReadyServerRpc → All Ready 분기)
m_AlivePlayerCount = Managers.Network.GetConnectedPlayerCount();

[Rpc(SendTo.Server)]
public void NotifyPlayerDiedServerRpc()
{
    if (m_GameState != GameState.Playing) return;

    m_AlivePlayerCount--;
    GameDebug.Log($"[InGameController] Alive: {m_AlivePlayerCount}");

    if (m_AlivePlayerCount <= 0)
        TriggerGameOver();
}
```

## 9. 엣지 케이스

| 케이스 | 처리 |
|--------|------|
| 같은 플레이어 OnDeathClientRpc 두 번 발화 | NotifyPlayerDiedServerRpc 안에서 `m_GameState != Playing`이면 무시 + `m_AlivePlayerCount`가 음수가 되도 `<= 0` 체크로 안전. 추가로 PlayerCharacter에 `m_HasReportedDeath` 플래그로 중복 보고 방지 |
| 클리어 직후 사망 (`GameState != Playing`) | `NotifyPlayerDiedServerRpc`에서 `m_GameState != Playing` 체크로 무시 |
| 사망 후 EscapePoint 도달 | `OnPlayerReachedEscape`에서 `IsDead` 체크 추가하면 안전. 본 스펙 범위 밖이지만 한 줄로 추가 |
| 본인이 카메라 타겟이 아님 | 카메라 전환 스킵 (다른 동료를 이미 보고 있는 상태) |

## 10. QA 시나리오 — `TestPlayerDeathAndGameOver`

1. 게임 시작 시 `m_AlivePlayerCount == 1`
2. 플레이어 `TakeDamage(99999)` → `IsDead == true`
3. 사망 후 `Attack()` 호출 → `m_AnimationComponent.IsAttacking()` 변화 없음 (가드 통과 안 함)
4. 사망 후 `MoveWithDirection(...)` → 위치 변화 없음
5. `m_AlivePlayerCount == 0` 확인 (reflection)
6. `InGameController.GameState == GameState.GameOver` 확인

## 11. 후속 작업

- 게임오버 화면 UI (M3 Week 4)
- "개인 탈출" 방식 검토 (GDD 기획)
- 사망 후 자유 카메라 / 관전자 UI (옵션)
