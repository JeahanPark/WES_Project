---
name: dev-qa
description: Use when developing features that need runtime QA testing in Unity. Runs the full dev → QA → verify cycle using MCP tools and TestManager.
---

# Dev-QA Workflow

기능 개발부터 Unity 플레이모드 런타임 검증까지의 전체 사이클.

## 1. 개발 단계

### 코드 작업
- 기능 구현 (스크립트, 프리팹, Inspector 설정 등)
- MCP 도구 활용: `u_editor_component`, `u_editor_reference`, `u_editor_prefab`, `u_editor_gameobject` 등
- 독립적인 코드 작업이 여러 개면 서브에이전트 병렬 처리 가능

### QA 대상 코드 스캔 (필수)
TestManager 시나리오를 작성하기 **전에** QA 대상 기능의 코드를 모두 읽는다.

**스캔 대상**:
- **진입점**: 해당 기능의 public 메서드, 이벤트 핸들러, UI 버튼 OnClick
- **상태 분기**: if/switch 조건, 상태 전이 (예: `if (m_HP <= 0)`, `switch (m_State)`)
- **데이터 경로**: 어떤 아이템/수치/상태 변화가 발생하는지
- **외부 의존성**: 다른 Manager/Worker/Component와의 상호작용

**산출물**: 스캔 결과를 바탕으로 **모든 분기를 커버하는 시나리오 목록**을 도출한다.
러프한 1개 시나리오 금지 — 분기 개수만큼 시나리오를 쪼갠다.

### TestManager 시나리오 추가
- 위 스캔에서 도출한 시나리오 목록을 `Test<기능명>_<분기명>()` 형식으로 각각 `Assets/Scripts/Manager/TestManager.cs`에 추가
- **원칙**: 테스트 전용 로직 금지. 기존 public 메서드를 조합만 한다.
- **커버리지 원칙**: 스캔한 분기/조건/상태 전이가 시나리오 목록에 모두 있어야 한다. 누락된 분기가 있다면 시나리오를 추가할지, 해당 분기가 도달 불가한지 판정한다.
- 예: `TestCraftItem_Success()`, `TestCraftItem_InsufficientMaterial()`, `TestCraftItem_InventoryFull()`

### 컴파일 확인
- `u_editor_asset(action: refresh)` 호출
- `u_console`으로 컴파일 에러 확인
- 에러 있으면 수정 후 재확인

## 2. QA 단계

### 플레이모드 진입
```
u_play_control(action: "enter")
```

### 시나리오 실행
스캔에서 도출한 모든 시나리오를 순차 실행한다. 단일 시나리오로 끝내지 않는다.
```
u_play_invoke(target: "TestManager", componentType: "TestManager", methodName: "Test<기능명>_<분기명>")
```

### 결과 검증
순서대로 실행:
1. `u_editor_query(action: find)` — 예상 오브젝트 존재/소멸 확인
   - **주의**: `DontDestroyOnLoad` 오브젝트(Managers, TestManager 등)는 검색 불가. `u_play_invoke` 호출 성공 여부로 존재를 확인한다.
2. `u_console` — 런타임 에러 확인
3. `u_screenshot` — 시각적 상태 캡처 (참고용)

### 플레이모드 종료
```
u_play_control(action: "exit")
```

## 3. 판정

### 자동 수정 후 재시도 (개발 단계로 복귀)
- 코드 에러, 런타임 에러, 예외
- MCP 도구 부족 → `u_play_invoke`으로 우회 시도
- 오브젝트 미생성, 상태 이상
- Inspector 참조 누락

### 중단 — 사용자에게 보고
- **기획적으로 시나리오 진행이 불가능한 경우만**
  - 예: 공격 수단이 없어서 몬스터를 죽일 수 없다
  - 예: 재료 획득 경로가 구현되지 않았다
  - 예: UI 흐름 자체가 설계되지 않았다

### 완료
- 콘솔 에러 없음 + 예상 결과 확인 → 자동 완료 처리

## 4. 세션 양도

MCP 자체 수정이 불가피하여 세션을 이어갈 수 없는 경우, `/rename`으로 세션 이름을 붙이고 사용자에게 재빌드 후 `/resume`으로 복귀하도록 안내한다.
