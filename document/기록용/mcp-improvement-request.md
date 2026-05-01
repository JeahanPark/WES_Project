# MCP Unity Plugin 기능 개선 요청

> 그리드 인벤토리 + 퀵슬롯 HUD 프리팹 설정 작업 중 발견된 한계

---

## 1. UI GameObject 생성 불가

**문제**: `add_gameobject`로 Canvas 하위에 오브젝트를 생성하면 일반 `Transform`만 붙고, UI에 필요한 `RectTransform` + `CanvasRenderer`가 생성되지 않음.

**재현**:
```
도구: u_editor_gameobject
action: "add"
target: "CellTemplate"              # Canvas 하위 UI 오브젝트
gameObjectName: "SelectedFrame"
prefabPath: "Assets/GameResource/UI/Popup/InventoryPopup.prefab"

결과: SelectedFrame에 Transform만 붙음 → Image 컴포넌트 추가 시도 시 실패
      (RectTransform이 없어서 UI 컴포넌트 추가 불가)
```

**우회**: `duplicate`로 기존 UI 오브젝트를 복제한 후 수정

**개선안**: `add_gameobject`에 `isUI: true` 옵션 추가, 또는 부모가 RectTransform을 가진 경우 자동으로 RectTransform + CanvasRenderer를 추가

---

## 2. GameObject Reparent(부모 변경) 불가

**문제**: 생성된 오브젝트를 다른 부모로 이동하는 기능이 없음.

**재현**:
```
상황: InventoryPopup 프리팹에 DragPreview(Image)를 루트 하위에 추가하고 싶음.
      하지만 duplicate는 원본과 같은 부모에만 복제됨.

시도:
1. CellTemplate 하위의 IconImage를 duplicate → CellTemplate 하위에 복사됨
2. InventoryPopup 루트로 이동하고 싶지만 reparent 기능 없음
3. InventoryWindow를 duplicate → 전체 구조가 복사되어 너무 무거움

결과: 원하는 위치에 UI 오브젝트를 배치할 수 없음
```

**우회**: 포기하고 코드에서 nullable 처리 (DragPreview 없이 동작하도록)

**개선안**: `move_gameobject` 또는 `set_parent` 액션 추가
```
action: "set_parent"
target: "DragPreview"
newParent: "InventoryPopup"
prefabPath: "Assets/GameResource/UI/Popup/InventoryPopup.prefab"
```

---

## 3. 배열 타입 SerializeField 참조 연결 불가

**문제**: `set_reference`가 `QuickSlotCell[]` 같은 배열/리스트 타입 필드를 지원하지 않음.

**재현**:
```
도구: u_editor_component
action: "set_reference"
target: "QuickSlotHUD"
componentType: "QuickSlotHUD"
mappingsJson: [
  {"propertyName":"m_Cells","referenceTarget":"QuickSlotCell_0","referenceComponentType":"QuickSlotCell"},
  {"propertyName":"m_Cells","referenceTarget":"QuickSlotCell_1","referenceComponentType":"QuickSlotCell"},
  ...
]
prefabPath: "Assets/GameResource/UI/HUD/QuickSlotHUD.prefab"

결과: "0/8 applied. Failed: m_Cells: Field type 'QuickSlotCell[]' is not a supported reference type"
```

**우회**: 코드를 `[SerializeField] private QuickSlotCell[] m_Cells` → `GetComponentsInChildren<QuickSlotCell>(true)`로 변경

**개선안**: 배열/리스트 필드에 대해 여러 참조를 인덱스 순서대로 할당하는 기능 지원
```json
// 제안 형식
{"propertyName":"m_Cells[0]","referenceTarget":"QuickSlotCell_0","referenceComponentType":"QuickSlotCell"}
// 또는 한번에
{"propertyName":"m_Cells","referenceTargets":["QuickSlotCell_0","QuickSlotCell_1",...]}
```

---

## 4. 제네릭 베이스 클래스의 SerializeField 접근 불가

**문제**: 컴포넌트의 제네릭 베이스 클래스에 정의된 `[SerializeField]` 필드를 `set_property`로 설정할 수 없음.

**재현**:
```
클래스 구조:
  BaseScroll<TData> : ScrollRect
    [SerializeField] private float m_CellSize = 100f;
    [SerializeField] private float m_Spacing = 10f;

  InventoryScroll : BaseScroll<ItemData>

도구: u_editor_component
action: "set_property"
target: "InventoryScroll"
componentType: "InventoryScroll"
propertyName: "m_CellSize"
propertyValue: "80"
prefabPath: "Assets/GameResource/UI/Popup/InventoryPopup.prefab"

결과: "Field or property 'm_CellSize' not found on 'InventoryScroll'"
```

참고: ScrollRect 자체의 프로퍼티(`vertical`, `horizontal`)는 정상 동작함.

**우회**: 기본값(100) 사용. Inspector에서 수동 변경.

**개선안**: 필드 탐색 시 상속 체인을 순회하되, 제네릭 타입(`BaseScroll<ItemData>` → `BaseScroll'1`)도 포함하여 탐색

---

## 5. InputActionAsset 편집 불가

**문제**: Unity Input System의 `.inputactions` 에셋에 새 액션을 추가/수정하는 MCP 기능이 없음.

**재현**:
```
상황: InputManager에서 "QuickSlot1" ~ "QuickSlot8" 액션을 코드로 FindAction 하는데,
      InputSystem_Actions.inputactions 에셋에 해당 액션이 없어서 null 반환.
      에셋 파일에 액션을 추가해야 하지만 MCP로 불가능.

결과: InputManager.SetupQuickSlotActions()에서 8개 모두 warning 로그 발생
      "[InputManager] QuickSlot1 action not found in InputActionAsset. Skipping."
```

**우회**: Unity 에디터에서 수동으로 액션 추가 필요.

**개선안**: `u_editor_input` 도구 추가 또는 `.inputactions` JSON 파일 직접 편집 지원
```
action: "add_action"
actionMap: "Player"
actionName: "QuickSlot1"
bindingPath: "<Keyboard>/1"
actionType: "Button"
```

---

## 우선순위 제안

| 순위 | 항목 | 이유 |
|------|------|------|
| 1 | UI GameObject 생성 | UI 프리팹 작업의 기본 — 가장 빈번하게 필요 |
| 2 | Reparent | UI 레이아웃 구성에 필수 |
| 3 | 배열 참조 연결 | 배열 SerializeField가 매우 흔함 |
| 4 | 제네릭 베이스 필드 | 상속 구조가 깊은 프로젝트에서 필요 |
| 5 | InputActionAsset | 빈도 낮지만 Input System 사용 시 필요 |
