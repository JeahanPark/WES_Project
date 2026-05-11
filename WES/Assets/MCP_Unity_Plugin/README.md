# MCP Unity Plugin

Claude와 Unity Editor를 실시간으로 연동하는 에디터 플러그인.
MCP 서버로부터 Named Pipe 명령을 수신해 Unity Editor 내에서 직접 실행한다.

---

## 설치

`MCP_Unity_Plugin/Editor/` 폴더를 Unity 프로젝트의 `Assets/` 안에 복사.

## McpBridge 활성화

Unity Editor 메뉴에서:

**Tools > McpBridge > ▶ 시작**

> McpBridge가 꺼져 있으면 Unity Editor 조작이 필요한 도구는 타임아웃 오류가 발생한다.

> 스크립트 컴파일(도메인 리로드) 시 서버가 자동으로 중지된다. 컴파일 완료 후 다시 ▶ 시작을 눌러야 한다.

---

## 지원 도구

| 도구 | 설명 | Unity Editor 필요 |
|------|------|:-----------------:|
| `generate_ui_with_gpt` | 자연어로 UGUI 프리팹 파일 생성 | X |
| `u_editor_gameobject` | GameObject 관리 및 조회 | O |
| `u_editor_component` | 컴포넌트/참조/버튼 관리 | O |
| `u_editor_prefab` | 프리팹 인스턴스 배치 | O |
| `u_editor_asset` | 에셋 검색/정보/갱신 | O |
| `u_editor_scene` | 씬 열기/저장/생성 | O |
| `u_editor_tag_layer` | 태그/레이어 관리 | O |
| `u_editor_input` | InputActionAsset 액션 추가/제거/조회 | O |
| `u_editor_menu` | Editor 메뉴 항목 경로 실행 | O |
| `u_set_transform` | Transform 위치/회전/스케일 설정 | O |
| `u_screenshot` | Game View 스크린샷 캡처 | O |
| `u_editor_sceneview` | Scene View 캡처 + 카메라 시점 제어 | O |
| `u_play` | Play 모드 제어/UI 클릭/런타임 호출 | O |
| `u_console` | Unity 콘솔 로그 읽기 | O |

> **경로 문법**: `target` 파라미터에 `Parent/Child/GrandChild` 형식의 경로를 사용하면 동일 이름 오브젝트를 정확히 지정할 수 있다.

---

## 도구 상세

### u_editor_gameobject

GameObject를 관리하고 조회한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `action` | O | `add` / `delete` / `rename` / `set_active` / `duplicate` / `set_parent` / `find` / `get` / `hierarchy` |
| `target` | O | 대상 GameObject 이름 또는 경로 (find 시 검색 키워드) |
| `gameObjectName` | X | 새 이름 (`add` / `rename` / `duplicate` 전용) |
| `active` | X | `true` / `false` (`set_active` 전용, 기본값: true) |
| `prefabPath` | X | 프리팹 에셋 경로. 생략 시 현재 씬에서 검색. |
| `newParent` | X | 새 부모 GameObject 이름 (`set_parent` 전용) |
| `maxCount` | X | 최대 노드 수 (`hierarchy` 전용, 기본값: 500) |

```
# 예시
action: "add",        target: "Canvas",  gameObjectName: "NewPanel"
action: "delete",     target: "OldPanel"
action: "rename",     target: "Panel",   gameObjectName: "MainPanel"
action: "set_active", target: "Popup",   active: false
action: "duplicate",  target: "ItemSlot", gameObjectName: "ItemSlot_Copy"
action: "set_parent", target: "ChildGO", newParent: "NewParentGO"
action: "find",       target: "Button"
action: "get",        target: "Canvas/Panel/Title"
action: "hierarchy"   (target 생략 시 전체 씬)
action: "hierarchy",  prefabPath: "Assets/Prefabs/UI/MyPopup.prefab"
```

---

### u_editor_component

컴포넌트 관리, Inspector 참조 연결, 버튼 onClick 연결을 처리한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `action` | O | `add` / `remove` / `set_property` / `list` / `set_reference` / `connect_button` |
| `target` | O | 대상 GameObject 이름 |
| `componentType` | X | 컴포넌트 타입 이름 (예: `BoxCollider`) |
| `prefabPath` | X | 프리팹 에셋 경로 |
| `propertyName` | X | 필드 이름 (`set_property` 전용) |
| `propertyValue` | X | 필드 값 (`set_property` 전용) |
| `mappingsJson` | X | 참조 매핑 JSON 배열 (`set_reference` 전용) |
| `listenerTarget` | X | 리스너 GameObject (`connect_button` 전용) |
| `listenerComponent` | X | 리스너 컴포넌트 타입 (`connect_button` 전용) |
| `methodName` | X | 메서드 이름 (`connect_button` 전용) |

**set_reference mappingsJson 형식:**
```json
[
  {"propertyName": "m_CloseButton",  "referenceTarget": "CloseButton"},
  {"propertyName": "m_Icon",         "referenceTarget": "Assets/Sprites/icon.png"},
  {"propertyName": "m_Scroll",       "referenceTarget": "ScrollView", "referenceComponentType": "ScrollRect"}
]
```

```
# 예시
action: "list",           target: "MyObject"
action: "add",            target: "Player",    componentType: "BoxCollider"
action: "set_property",   target: "Player",    componentType: "HealthComponent", propertyName: "m_MaxHp", propertyValue: "100"
action: "set_reference",  target: "MyPopup",   componentType: "MyPopup", mappingsJson: "[...]", prefabPath: "Assets/Prefabs/UI/MyPopup.prefab"
action: "connect_button", target: "CloseButton", listenerTarget: "MyPopup", listenerComponent: "MyPopup", methodName: "OnClickClose"
```

---

### u_editor_prefab

씬 또는 프리팹의 특정 GameObject 하위에 프리팹 인스턴스를 배치한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `prefabPath` | O | 배치할 프리팹 에셋 경로 |
| `parentTarget` | O | 부모로 사용할 GameObject 이름 |
| `parentPrefabPath` | X | 부모가 프리팹 내부에 있을 경우 해당 프리팹 경로 |

---

### u_editor_asset

에셋을 검색하거나 정보를 조회하고, AssetDatabase를 갱신한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `action` | O | `find` / `get_info` / `refresh` |
| `filter` | X | 검색 필터 (`find` 전용, 예: `"t:Prefab Button"`) |
| `folder` | X | 검색 폴더 (`find` 전용, 예: `"Assets/Prefabs"`) |
| `assetPath` | X | 에셋 경로 (`get_info` / `refresh` 전용) |

```
# 예시
action: "find",     filter: "t:Prefab", folder: "Assets/Prefabs/UI"
action: "get_info", assetPath: "Assets/Scripts/MyScript.cs"
action: "refresh"
action: "refresh",  assetPath: "Assets/Scripts/MyScript.cs"
```

---

### u_editor_scene

씬을 열거나 저장하거나 새로 생성한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `action` | O | `open` / `save` / `create` |
| `scenePath` | X | 씬 경로 (`save` 시 생략하면 현재 씬 저장) |

```
# 예시
action: "open",   scenePath: "Assets/Scenes/Main.unity"
action: "save"
action: "create", scenePath: "Assets/Scenes/NewScene.unity"
```

---

### u_editor_tag_layer

태그와 레이어를 관리한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `type` | O | `tag` / `layer` |
| `action` | O | tag: `list` / `add` / `remove` / `set`, layer: `list` / `set` / `remove` / `set_object` |
| `tagName` | X | 태그 이름 (tag 전용) |
| `layerIndex` | X | 레이어 인덱스 (layer 전용, 기본값: 0) |
| `layerName` | X | 레이어 이름 (layer 전용) |
| `target` | X | GameObject 이름 (`set` / `set_object` 전용) |

```
# 예시
type: "tag",   action: "list"
type: "tag",   action: "add",  tagName: "Enemy"
type: "tag",   action: "set",  tagName: "Player", target: "Hero"
type: "layer", action: "list"
type: "layer", action: "set",  layerIndex: 8, layerName: "Interactive"
type: "layer", action: "set_object", target: "Hero", layerName: "Player"
```

---

### u_set_transform

GameObject의 Transform(위치/회전/스케일)을 설정한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `target` | O | 대상 GameObject 이름 |
| `mode` | X | `editor` (기본값) / `play` |
| `posX`, `posY`, `posZ` | X | 위치 좌표 |
| `rotX`, `rotY`, `rotZ` | X | 회전 (Euler) |
| `scaleX`, `scaleY`, `scaleZ` | X | 스케일 |

```
# 예시
target: "Player", posX: 0, posY: 1, posZ: 0
target: "Camera", rotX: 30, rotY: 45, rotZ: 0, mode: "play"
target: "Item",   scaleX: 2, scaleY: 2, scaleZ: 2
```

---

### u_screenshot

Game View 스크린샷을 파일로 저장한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `u_screenshotPath` | X | 출력 경로. 생략 시 프로젝트 루트에 저장. |

---

### u_editor_sceneview

Scene View(씬뷰) 캡처 및 카메라 시점 제어를 통합 처리한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `subAction` | O | `screenshot` / `focus` / `preset` / `get` |
| `target` | △ | `focus` 대상 GameObject 이름/경로 (`focus` 필수) |
| `view` | △ | 시점 (`top` / `front` / `side` / `persp`) — `preset` 필수 |
| `angle` | X | `focus` 각도 — 동일 enum, 기본값 `persp` |
| `size` | X | `preset` 거리 (생략 시 현재 size 유지) |
| `screenshotPath` | X | `screenshot` 저장 경로 (생략 시 프로젝트 루트의 `screenshot_sceneview.png`) |

```
# 예시
subAction: "screenshot"
subAction: "screenshot",  screenshotPath: "C:/temp/sceneview.png"
subAction: "focus",       target: "Player"
subAction: "focus",       target: "Player",          angle: "top"
subAction: "preset",      view: "top",               size: 100
subAction: "get"
```

**시점 매핑:**
- `top` — 위에서 아래
- `front` — 정면 (-Z 응시)
- `side` — 측면 (-X 응시)
- `persp` — 일반 원근 (Euler 30, 45, 0)

`focus`는 대상의 Renderer bounds로 자동 거리를 계산해 카메라를 정렬한다.
`preset`은 현재 pivot을 유지하며 시점만 전환한다.
`get`은 현재 씬뷰의 pivot/rotation/size 등 상태를 JSON으로 반환한다.

---

### u_play

Play 모드 제어, UI 버튼 클릭, 런타임 메서드 호출을 처리한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `action` | O | `control` / `click` / `invoke` |
| `sub_action` | X | `enter` / `exit` / `status` (`control` 전용) |
| `target` | X | GameObject/Button 이름 (`click` / `invoke` 전용) |
| `componentType` | X | 컴포넌트 타입 (`invoke` 전용) |
| `methodName` | X | 메서드 이름 (`invoke` 전용) |
| `args` | X | 메서드 인자 (`invoke` 전용) |

```
# 예시
action: "control", sub_action: "enter"
action: "control", sub_action: "status"
action: "click",   target: "StartButton"
action: "invoke",  target: "GameManager", componentType: "GameManager", methodName: "ResetScore"
```

---

### u_console

Unity 콘솔 로그를 읽어 반환한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `logType` | X | `error` / `warning` / `log` / `all` (기본값: `all`) |
| `maxCount` | X | 최대 항목 수 (기본값: 50, 최신 항목부터) |

```
# 예시
logType: "error", maxCount: 20
```

---

### u_editor_input

InputActionAsset(`.inputactions`) 파일의 액션을 추가/제거/조회한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `action` | O | `add_action` / `remove_action` / `list_actions` |
| `assetPath` | O | `.inputactions` 파일 경로 (예: `Assets/Settings/InputSystem_Actions.inputactions`) |
| `actionMap` | X | ActionMap 이름 (예: `Player`) — add/remove 필수 |
| `actionName` | X | 액션 이름 (예: `QuickSlot1`) — add/remove 필수 |
| `actionType` | X | `Button` / `Value` / `PassThrough` (기본값: `Button`, `add_action` 전용) |
| `bindingPath` | X | 바인딩 경로 (예: `<Keyboard>/1`, `add_action` 전용) |

```
# 예시
action: "list_actions",   assetPath: "Assets/Settings/InputSystem_Actions.inputactions"
action: "add_action",     assetPath: "Assets/Settings/InputSystem_Actions.inputactions",
                          actionMap: "Player", actionName: "QuickSlot1",
                          actionType: "Button", bindingPath: "<Keyboard>/1"
action: "remove_action",  assetPath: "Assets/Settings/InputSystem_Actions.inputactions",
                          actionMap: "Player", actionName: "QuickSlot1"
```

---

### u_editor_menu

Unity Editor 메뉴 항목을 경로로 실행한다. 커스텀 에디터 도구(NavMesh 베이크, 맵 재생성 등) 트리거에 유용하다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `menuPath` | O | 메뉴 경로 (슬래시 구분, 예: `Tools/Map Generator/Bake NavMesh`, `File/Save`) |

```
# 예시
menuPath: "Tools/Map Generator/Bake NavMesh"
menuPath: "File/Save"
```

---

### generate_ui_with_gpt

자연어 프롬프트로 UGUI 프리팹 파일을 생성한다. Unity Editor 연결 불필요.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `unityProjectRoot` | O | Unity 프로젝트 루트 경로 (Assets/ 포함 폴더) |
| `outputPrefabPath` | O | 출력 프리팹 경로 (예: `Assets/UI/Generated/Test.prefab`) |
| `guidTablePath` | O | guidTable.json 경로 |
| `prompt` | O | UI 설명 (자연어) |
| `referenceWidth` | X | 레퍼런스 해상도 너비 (기본값: 1920) |
| `referenceHeight` | X | 레퍼런스 해상도 높이 (기본값: 1080) |

---

## 새 핸들러 추가 방법

MCP 서버 측 도구 추가 및 Unity 핸들러 확장 방법은
[MCP_Unity/README.md](../README.md) 참고.
