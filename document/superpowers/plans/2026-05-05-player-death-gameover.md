---
작성일: 2026-05-05
스펙: ../specs/2026-05-05-player-death-gameover-design.md
---

# 구현 계획 — 플레이어 사망 & 게임오버

## Task 1: CharacterRegistry 헬퍼 추가
`GetAlivePlayers()` 메서드 — 살아있는 PlayerCharacter 리스트 반환.

## Task 2: CharacterBase MoveWithDirection 가드
`if (IsDead) return;` 한 줄 추가.

## Task 3: PlayerCharacter
- `Attack()` / `Interact()` 시작부에 `if (IsDead) return;` 추가
- `OnDeath()` override:
  - 본인(`IsOwner`)이면:
    - 카메라 타겟이 자기 transform이면 → 살아있는 동료로 전환 (없으면 그대로)
    - `InGameController.Instance.NotifyPlayerDiedServerRpc()` 호출
- 중복 보고 방지: `m_HasReportedDeath` 로컬 플래그

## Task 4: InGameController
- `private int m_AlivePlayerCount = 0;`
- `NotifyClientReadyServerRpc`의 "All clients ready" 분기에서 `m_AlivePlayerCount = Managers.Network.GetConnectedPlayerCount();` 초기화
- `[Rpc(SendTo.Server)] public void NotifyPlayerDiedServerRpc()` — 카운트 감소 + 0이면 `TriggerGameOver()`
- (보너스) `OnPlayerReachedEscape`에 `if (_player.IsDead) return;` 추가 — 안전 가드

## Task 5: TestManager QA 시나리오
`TestPlayerDeathAndGameOver`:
1. 살아있는 카운트 초기값 확인 (reflection)
2. `player.TakeDamage(99999, player)` → `IsDead == true` 확인
3. 사망 후 `Attack()` 호출 → 데미지 발생 안 함 (적 HP 변화 없음 — 또는 IsDead 체크로 직접 검증)
4. 사망 후 `MoveWithDirection(Vector2.right)` 1초 → 위치 변화 ≤ 0.1f
5. `m_AlivePlayerCount == 0` (reflection)
6. `controller.GameState == GameState.GameOver`

## Task 6: InGameController에 thin wrapper
`TestPlayerDeathAndGameOver()` 추가 (TestManager로 위임).

## Task 7: 컴파일 + QA + 커밋
- u_editor_asset refresh
- u_play enter → invoke → 결과 확인
- 게임오버 후 GameState 확인 → 3초 후 로비 씬 로드되므로 시나리오 6은 GameOver 진입만 확인
