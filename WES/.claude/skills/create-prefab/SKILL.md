---
name: create-prefab
description: MCP 도구로 UI 프리팹 생성 → 컴포넌트 추가 → 참조 매핑을 순차 실행하는 워크플로우
argument-hint: "[UI 설명]"
disable-model-invocation: true
allowed-tools: mcp__mcp-unity__generate_ui_with_gpt, mcp__mcp-unity__manage_components, mcp__mcp-unity__set_reference
---

# MCP 프리팹 생성 워크플로우

3단계를 **순서대로** 실행한다. 어느 단계든 실패하면 **즉시 중단**하고 완료된 내용을 보고한다.

## 고정 파라미터

- `unityProjectRoot`: `c:/GitFork/WES_Project/WES`
- `guidTablePath`: `MCP/guidTable.json`
- 기본 해상도: `1920 × 1080`

---

## Step 1: 프리팹 생성 (`generate_ui_with_gpt`)

사용자에게 아래 정보를 확인한다 (없는 경우에만 물어본다):

1. **출력 경로** — 예: `Assets/GameResource/UI/Popup/MyPopup.prefab`
2. **UI 설명** — `$ARGUMENTS`가 있으면 그대로 사용

`generate_ui_with_gpt`를 호출한다.

**실패 시** → 즉시 중단하고 아래 포맷으로 보고:

```
❌ Step 1 실패: 프리팹 생성
원인: [오류 메시지]

완료된 단계: 없음
미완료 단계: Step 2 (컴포넌트 추가), Step 3 (참조 매핑)
```

---

## Step 2: 컴포넌트 추가 (`manage_components`)

> Unity Editor가 열려 있고 McpBridge가 활성화되어 있어야 한다.
> (Unity Editor > Tools > McpBridge > Start)

사용자에게 아래 정보를 확인한다:

- **대상 GameObject 이름** (루트 오브젝트)
- **추가할 컴포넌트 목록** — 예: `InventoryPopup`, `ScrollRect`
- **프리팹 경로** — Step 1 결과를 사용

각 컴포넌트마다 `manage_components` (action: `"add"`)를 순서대로 호출한다.

**실패 시** → 즉시 중단하고 아래 포맷으로 보고:

```
❌ Step 2 실패: 컴포넌트 추가 ([컴포넌트명])
원인: [오류 메시지]

완료된 단계: Step 1 ✅ ([프리팹 경로])
미완료 단계: Step 3 (참조 매핑)
수동 재시도: /create-prefab 을 다시 호출하거나 Step 2부터 직접 진행
```

---

## Step 3: 참조 매핑 (`set_reference`)

사용자에게 아래 정보를 확인한다:

- **컴포넌트 타입** — Step 2에서 추가한 스크립트 컴포넌트
- **매핑 목록** — `SerializeField 필드명` → `연결할 GameObject 이름 또는 에셋 경로`

`set_reference`를 **한 번** 호출로 모든 매핑을 처리한다.

**실패 시** → 즉시 중단하고 아래 포맷으로 보고:

```
❌ Step 3 실패: 참조 매핑
원인: [오류 메시지]

완료된 단계: Step 1 ✅, Step 2 ✅
미완료 단계: Step 3 (참조 매핑)
수동 재시도: set_reference 도구를 직접 호출
```

---

## 완료 보고

3단계 모두 성공 시 아래 포맷으로 요약한다:

```
✅ 프리팹 생성 완료

Step 1: [프리팹 경로]
Step 2: [추가된 컴포넌트 목록]
Step 3: [연결된 필드 목록]
```
