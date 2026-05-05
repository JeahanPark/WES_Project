---
작성일: 2026-05-05
스펙: ../specs/2026-05-05-popup-esc-ui-input-guard-design.md
---

# 구현 계획 — ESC 팝업 닫기 & UI 클릭 가드

## 작업 순서

### Task 1: PopupManager.CloseTop()

`Assets/Scripts/Manager/PopupManager.cs`

- `public void CloseTop()` 추가
  - `m_OpenedPopups.Count == 0` → return
  - 마지막 인덱스 팝업의 `Close()` 호출 (`Close(BasePopup)` 위임)

### Task 2: InputManager — Cancel 액션 + UI 가드

`Assets/Scripts/Manager/InputManager.cs`

- 필드 추가
  - `private InputAction m_CancelAction;`
  - `private readonly Subject<Unit> m_OnCancel = new Subject<Unit>();`
  - `public IObservable<Unit> OnCancelAsObservable => m_OnCancel;`
- `Init()`에서 `m_UIMap.FindAction("Cancel")` 바인딩 + `performed` 구독
- `Clear()`에서 구독/Subject 해제 추가
- `OnCancelPerformed` 메서드 추가 → `m_OnCancel.OnNext(Unit.Default)`
- `OnAttackPerformed`, `OnInteractPerformed` 시작부에 가드:
  ```csharp
  if (IsPointerOverUI()) return;
  ```
- `private static bool IsPointerOverUI()` 헬퍼 추가
  - `using UnityEngine.EventSystems;` 추가
  - `EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()`

### Task 3: InGameController에 OnCancel → PopupManager.CloseTop 와이어링

`Assets/Scripts/Controller/InGameController.cs`

- `Awake` 또는 입력 구독 시점에:
  ```csharp
  Managers.Input.OnCancelAsObservable
      .Subscribe(_ => Managers.Popup.CloseTop())
      .AddTo(this);
  ```
- 기존 입력 구독 위치 근처에 배치

### Task 4: TestManager QA 시나리오

`Assets/Scripts/Manager/TestManager.cs`

- `public void TestPopupEscapeAndUIGuard()` 추가 → `StartCoroutine(CoTestPopupEscapeAndUIGuard())`
- 시나리오:
  1. 빈 스택에서 `CloseTop()` 호출 → 예외 없음
  2. `InventoryPopup` 열기 → `CloseTop()` → `FindOpen<InventoryPopup>() == null`
  3. `InventoryPopup` + `CraftPopup` 스택 → `CloseTop()` → `CraftPopup`만 닫힘, `InventoryPopup` 유지
  4. (마우스 시뮬) 화면 중앙 좌표 = 인벤토리 영역 위로 마우스 이동 → `EventSystem.IsPointerOverGameObject()` 검증 → InputManager 게이트가 `OnAttack`을 발화하지 않는지 확인
  5. 팝업 닫고 빈 영역으로 마우스 이동 → `IsPointerOverGameObject() == false` 확인 → `OnAttack` 정상 발화

### Task 5: 컴파일 + QA 실행

- `u_editor_asset(action: refresh)` → 컴파일 강제
- `u_console`로 컴파일 에러 확인
- `u_play(mode: enter)` → 플레이모드 진입
- `u_play_invoke(method: TestPopupEscapeAndUIGuard)` 또는 메뉴 호출
- `u_console`로 PASS/FAIL 결과 확인
- 회귀: 기존 시나리오 1개 (`TestCriticalHit` 또는 `TestBuilding`) 재실행해서 회귀 없음 확인

### Task 6: 커밋

- 한국어 메시지, Co-Authored-By 제외
- 메시지: `UX: ESC로 팝업 닫기 + UI 위 클릭의 인게임 입력 차단`
- 본문: 추가된 항목 bullet
