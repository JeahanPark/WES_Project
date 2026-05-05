---
작성일: 2026-05-05
주제: ESC 팝업 닫기 + UI 위 클릭의 인게임 입력 차단
상태: 승인됨 (옵션 A)
---

# ESC 팝업 닫기 & UI 클릭 가드 — 설계 문서

## 1. 배경

현재 UX 결함 두 가지:

1. **팝업을 ESC로 닫을 수 없다.** PopupManager는 스택을 가지고 있지만 ESC 핸들러가 어디에도 없다.
2. **UI 위 클릭이 인게임 입력으로 새어 나간다.** 인벤토리 토글 버튼, 퀵슬롯 셀, 크래프트 패널 등을 클릭하면 같은 좌클릭이 `Player/Attack` 액션으로 발화해 캐릭터가 공격 모션을 재생한다.

두 결함은 둘 다 “UI 입력과 게임 입력을 한 곳에서 게이트하지 않는다”는 동일한 원인에서 비롯되므로 한 번에 해결한다.

## 2. 목표

- `ESC` 키 → 최상단 팝업 1개 닫기 (스택 보존)
- 마우스 포인터가 UI 위에 있는 동안 `Attack` / `Interact` 입력 발화 차단
- 건물 배치(`BuildingPlacementWorker`)는 이미 자체 가드가 존재 → 변경 없음
- 입력 게이팅 정책을 **InputManager 한 곳**에 모아두어 향후 신규 입력 추가 시 자동으로 보호되도록 한다

## 3. 비목표 (Out of scope)

- 일시정지/옵션 메뉴 도입 (별도 기획)
- 백드롭(Dim) 클릭으로 닫기 — 실수로 닫히는 위험 회피
- 키보드 단축키(인벤토리 토글 `I`, 퀵슬롯 `1~8`) 차단 — 마우스 호버와 무관한 별도 이슈
- 팝업 열렸을 때 `Move(WASD)` 차단 — 인벤토리 보면서 이동하는 일반적 패턴 유지

## 4. 결정된 동작

### 4.1 ESC 처리

| 상황 | 결과 |
|------|------|
| 팝업 1개 이상 열려 있음 | 최상단 1개만 `Close()` |
| 팝업 없음 | no-op (무시) |
| 건물 배치 모드 + 팝업 동시 활성화 | 두 핸들러가 모두 발화. `BuildingPlacementWorker`는 자체 ESC를 유지하고, PopupManager는 새 ESC 구독을 추가. 동시 활성화 자체가 흔하지 않은 시나리오이며, 둘 다 닫히는 것이 사용자의 “ESC = 모드 빠져나오기” 직관과 부합한다. |

### 4.2 UI 클릭 가드

`Managers.Input.OnAttackAsObservable` / `OnInteractAsObservable`은 이벤트 발행 직전에 `EventSystem.IsPointerOverGameObject()`를 검사한다. UI 위면 발행 자체를 스킵 → 모든 구독자(현재는 `PlayerCharacter`)가 자동으로 보호받는다.

| 마우스 위치 | Attack 발화 | Interact 발화 |
|-------------|-------------|---------------|
| 월드 위 | ✅ | ✅ |
| 팝업 위 | ❌ | ❌ |
| HUD 버튼 위 (인벤토리 토글, 퀵슬롯 셀, 크래프트 탭 등) | ❌ | ❌ |

### 4.3 건물 배치

`BuildingPlacementWorker`는 이미 다음을 처리한다 (변경 없음):
- `Input.GetMouseButtonDown(0) && !IsPointerOverUI()` — UI 위 클릭 무시
- `Input.GetKeyDown(KeyCode.Escape)` — 배치 취소

## 5. 변경 범위

| 파일 | 변경 |
|------|------|
| `Assets/Scripts/Manager/InputManager.cs` | • `m_CancelAction` 필드 + `m_OnCancel` Subject 추가<br>• `OnCancelAsObservable` 노출<br>• `OnAttackPerformed` / `OnInteractPerformed`에 `IsPointerOverUI()` 가드 추가 |
| `Assets/Scripts/Manager/PopupManager.cs` | • `CloseTop()` 메서드 추가 (스택 마지막 1개만 닫음, 빈 스택은 no-op) |
| `Assets/Scripts/Controller/InGameController.cs` | • `OnEnable`/`OnDisable` 또는 동등 시점에 `Managers.Input.OnCancelAsObservable.Subscribe(_ => Managers.Popup.CloseTop()).AddTo(this)` 와이어링 |
| `Assets/Scripts/Manager/TestManager.cs` | • `TestPopupEscapeAndUIGuard()` 시나리오 추가 (PASS/FAIL 카운팅) |

비변경:
- `PlayerCharacter`, `BuildingPlacementWorker`, HUD/팝업 컴포넌트 — 모두 무수정

## 6. 데이터 흐름

```
[ESC 키]
  Unity InputSystem (UI/Cancel 액션, Keyboard)
    └─> InputManager.OnCancelPerformed
          └─> m_OnCancel.OnNext(Unit.Default)
                └─> InGameController 구독 콜백
                      └─> PopupManager.CloseTop()
                            └─> m_OpenedPopups[last].Close()

[마우스 좌클릭]
  Unity InputSystem (Player/Attack 액션, Mouse Left)
    └─> InputManager.OnAttackPerformed
          ├─ if (IsPointerOverUI()) return  ← 여기서 차단
          └─> m_OnAttack.OnNext(Unit.Default)
                └─> PlayerCharacter.Attack()
```

`IsPointerOverUI()` 구현:
```csharp
private static bool IsPointerOverUI()
{
    var es = EventSystem.current;
    return es != null && es.IsPointerOverGameObject();
}
```

EventSystem이 없는 씬에서는 `false`를 반환 → 입력은 정상 통과 (안전한 디폴트).

## 7. 엣지 케이스

| 케이스 | 처리 |
|--------|------|
| ESC 빈 스택에서 호출 | `CloseTop`은 `m_OpenedPopups.Count == 0`이면 즉시 return |
| ESC 두 번 빠르게 누름 | 두 팝업이 위→아래 순으로 닫힘 (의도된 동작) |
| EventSystem 없는 씬 (Intro 등) | `IsPointerOverUI()` false → 입력 정상 통과 |
| `DisablePlayerInput()` 상태 | InputSystem 액션 자체가 발화 안 하므로 게이트도 호출되지 않음 — 영향 없음 |
| `InventoryPopup` 안의 슬롯 드래그 | 드래그는 `EventSystem`이 처리하는 UI 이벤트 — `Attack` 액션 발화는 가드로 차단됨, 드래그 자체는 정상 동작 |
| 건물 배치 모드 + 팝업 동시 활성화 + ESC | 두 핸들러 모두 발화 (배치 취소 + 팝업 닫기). 의도적으로 허용. |

## 8. QA 시나리오

`TestManager.TestPopupEscapeAndUIGuard()`로 PASS/FAIL 카운팅:

1. **빈 스택 ESC** — `Managers.Popup.CloseTop()` 호출해도 예외 없이 통과
2. **단일 팝업 ESC** — `InventoryPopup` 열고 `CloseTop()` → 닫힘 확인
3. **스택 ESC** — `InventoryPopup` 위에 `CraftPopup` 쌓고 `CloseTop()` → CraftPopup만 닫힘, InventoryPopup 유지
4. **UI 위 Attack 가드** — InventoryPopup 열린 상태에서 `OnAttackPerformed` 강제 호출 시 `Attack()`이 발생하지 않는지 검증 (Attack 카운터 변화 없음)
5. **월드 Attack 정상** — 팝업 모두 닫고 `OnAttackPerformed` 호출 시 `Attack()` 정상 발생

`EventSystem.IsPointerOverGameObject()`는 실제 마우스 위치를 본다. 자동 테스트에서는 마우스를 강제로 UI 위에 둘 수 없으므로 시나리오 4/5는 **`IsPointerOverUI()`를 통과/미통과 양쪽으로 직접 검증**하는 형태로 구성한다 — 즉, 팝업이 열려 있고 화면 중앙에 그것이 가시화된 상태에서 `EventSystem.current.IsPointerOverGameObject()`의 직접 값을 점검하고, Attack 호출 카운트와 비교한다.

테스트 한계: 마우스 위치를 InputSystem으로 정확히 시뮬레이션하면 EventSystem이 다음 프레임에 그것을 반영하므로, 시나리오 4/5에서는 `Mouse.current.WarpCursorPosition(...)` 후 1~2프레임 대기하는 흐름으로 처리.

## 9. 리스크

- **InputManager가 EventSystem에 의존**한다는 단방향 결합이 추가된다. 그러나 EventSystem은 Unity의 기본 UI 인프라이며, IsPointerOverGameObject는 EventSystem 없으면 false를 반환하므로 비-UI 씬(Intro 등)에서도 안전하다.
- **마우스 위치 시뮬레이션 한계** — QA 시나리오 4/5는 `IsPointerOverGameObject` 직접값 검증으로 우회한다.

## 10. 후속 작업 (이번 스펙의 범위 밖)

- 일시정지/옵션 메뉴 도입 시 ESC 정책을 “팝업 없음 + 옵션 메뉴 열기”로 확장
- 백드롭 클릭으로 닫기 옵션을 BasePopup 플래그로 토글 가능하게 노출
