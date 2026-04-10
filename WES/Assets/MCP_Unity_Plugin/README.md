# MCP Unity Plugin

Claude와 Unity Editor를 실시간으로 연동하는 에디터 플러그인.
MCP 서버로부터 TCP 명령을 수신해 Unity Editor 내에서 직접 실행한다.

---

## 설치

`MCP_Unity_Plugin/Editor/` 폴더를 Unity 프로젝트의 `Assets/` 안에 복사.

```
Assets/
└── Editor/
    ├── McpBridge.cs
    └── McpBridge/
        ├── McpBridgeComponents.cs
        ├── McpBridgeReferences.cs
        ├── McpBridgeInstantiate.cs
        ├── McpBridgeGameObject.cs
        ├── McpBridgeButton.cs
        └── McpBridgeRefresh.cs
```

## McpBridge 활성화

Unity Editor 메뉴에서:

**Tools > McpBridge > ▶ 시작**

> McpBridge가 꺼져 있으면 Unity Editor 조작이 필요한 도구는 타임아웃 오류가 발생한다.

> 스크립트 컴파일(도메인 리로드) 시 서버가 자동으로 중지된다. 컴파일 완료 후 다시 ▶ 시작을 눌러야 한다.

---

## 지원 도구

| 도구 | 설명 | Unity Editor 필요 |
|------|------|:-----------------:|
| `echo` | MCP 서버 연결 상태 확인 | X |
| `generate_ui_with_gpt` | 자연어로 UGUI 프리팹 파일 생성 | X |
| `manage_components` | GameObject에 컴포넌트 추가/제거/설정/목록 | O |
| `set_reference` | Inspector SerializeField 참조 연결 | O |
| `instantiate_prefab` | 씬/프리팹 하위에 프리팹 인스턴스 배치 | O |
| `add_gameobject` | 씬/프리팹 하위에 새 빈 GameObject 추가 | O |
| `connect_button` | Button onClick에 메서드 연결 | O |
| `refresh_assets` | AssetDatabase 전체 갱신 또는 특정 에셋 reimport | O |

---

## 도구 상세

### manage_components

GameObject에 컴포넌트를 추가/제거하거나 필드 값을 설정한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `action`        | O | `add` / `remove` / `set_property` / `list` |
| `target`        | O | 대상 GameObject 이름 |
| `componentType` | X | 컴포넌트 타입 이름 (예: `BoxCollider`) |
| `prefabPath`    | X | 프리팹 에셋 경로. 생략 시 현재 씬에서 검색. |
| `propertyName`  | X | 설정할 필드 이름 (`set_property` 전용) |
| `propertyValue` | X | 설정할 값 (`set_property` 전용) |

```
# 예시
action: "list",         target: "MyObject", prefabPath: "Assets/Prefabs/MyObject.prefab"
action: "add",          target: "Player",   componentType: "BoxCollider"
action: "set_property", target: "Player",   componentType: "HealthComponent", propertyName: "m_MaxHp", propertyValue: "100"
```

---

### set_reference

Inspector 필드에 다른 컴포넌트 또는 에셋 참조를 연결한다.
한 번의 호출로 여러 필드를 동시에 매핑할 수 있다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `target`        | O | 필드를 가진 컴포넌트가 붙은 GameObject 이름 |
| `componentType` | O | 필드를 가진 컴포넌트 타입 |
| `mappingsJson`  | O | 매핑 목록 JSON 배열 문자열 |
| `prefabPath`    | X | 프리팹 에셋 경로. 생략 시 현재 씬에서 검색. |

**mappingsJson 형식:**
```json
[
  {"propertyName": "m_CloseButton",  "referenceTarget": "CloseButton"},
  {"propertyName": "m_Icon",         "referenceTarget": "Assets/Sprites/icon.png"},
  {"propertyName": "m_Scroll",       "referenceTarget": "ScrollView", "referenceComponentType": "ScrollRect"}
]
```

| 매핑 필드 | 필수 | 설명 |
|---------|:----:|------|
| `propertyName`           | O | 연결할 필드 이름 |
| `referenceTarget`        | O | 연결할 GameObject 이름 또는 `Assets/`로 시작하는 에셋 경로 |
| `referenceComponentType` | X | 생략 시 필드 타입 자동 추론 |

---

### instantiate_prefab

씬 또는 프리팹의 특정 GameObject 하위에 프리팹 인스턴스를 배치한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `prefabPath`       | O | 배치할 프리팹 에셋 경로 |
| `parentTarget`     | O | 부모로 사용할 GameObject 이름 |
| `parentPrefabPath` | X | 부모가 프리팹 내부에 있을 경우 해당 프리팹 경로 |

---

### add_gameobject

씬 또는 프리팹 하위에 새 빈 GameObject를 추가한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `parentTarget`   | O | 부모로 사용할 GameObject 이름 |
| `gameObjectName` | O | 새로 생성할 GameObject 이름 |
| `prefabPath`     | X | 부모가 프리팹 내부에 있을 경우 해당 프리팹 경로 |

```
# 예시
parentTarget: "ContentArea"
gameObjectName: "ItemSlot"
prefabPath: "Assets/Prefabs/UI/MyPopup.prefab"
```

---

### connect_button

Button 컴포넌트의 onClick 이벤트에 메서드를 Persistent Listener로 연결한다.
파라미터 없는 `void` 메서드만 지원한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `target`            | O | Button이 붙은 GameObject 이름 |
| `listenerTarget`    | O | 메서드를 가진 컴포넌트의 GameObject 이름 |
| `listenerComponent` | O | 메서드를 가진 컴포넌트 타입 이름 |
| `methodName`        | O | 연결할 메서드 이름 |
| `prefabPath`        | X | 프리팹 에셋 경로. 생략 시 현재 씬에서 검색. |

```
# 예시: CloseButton.onClick → MyPopup.OnClickClose
target: "CloseButton"
listenerTarget: "MyPopup"
listenerComponent: "MyPopup"
methodName: "OnClickClose"
prefabPath: "Assets/Prefabs/UI/MyPopup.prefab"
```

---

### refresh_assets

AssetDatabase를 전체 갱신하거나 특정 에셋을 reimport한다.

| 파라미터 | 필수 | 설명 |
|---------|:----:|------|
| `assetPath` | X | reimport할 에셋 경로. 생략 시 전체 AssetDatabase.Refresh() 실행. |

```
# 전체 갱신 (파라미터 없음)

# 특정 에셋 reimport
assetPath: "Assets/Scripts/MyScript.cs"
```

---

## 새 핸들러 추가 방법

MCP 서버 측 도구 추가 및 Unity 핸들러 확장 방법은
[MCP_Unity/README.md](../README.md) 참고.
