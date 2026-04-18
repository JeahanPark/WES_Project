# WES — MCP 테스트 도구 가이드

> **작성일**: 2026-04-14
> 이 문서는 MCP 도구를 이용해 Unity Play Mode에서 게임을 테스트하는 방법을 정리한다.
> 도구를 사용하려면 Unity Editor에서 **Tools > McpBridge > ▶ 시작** 이 활성화되어 있어야 한다.

---

## play_mode_control — Play Mode 제어

**MCP 도구**: `mcp__mcp-unity__play_mode_control`

Play Mode 진입 / 종료 / 상태 확인.

| 파라미터 | 필수 | 설명 |
|----------|:----:|------|
| `action` | O | `"enter"` / `"exit"` / `"status"` |

```
# Play Mode 진입
action: "enter"

# Play Mode 종료
action: "exit"

# 현재 상태 확인
action: "status"
→ { "isPlaying": true }
```

---

## set_position — 위치 이동

**MCP 도구**: `mcp__mcp-unity__set_position`

Play Mode 중 GameObject의 위치를 즉시 변경한다.
이동 입력 시뮬레이션은 매 프레임 값 유지가 필요하므로 지원하지 않는다.
테스트에서 "특정 위치에 있어야 한다"는 조건은 이 도구로 해결한다.

| 파라미터 | 필수 | 설명 |
|----------|:----:|------|
| `target` | O | 대상 GameObject 이름 |
| `x` | O | 월드 좌표 X |
| `y` | O | 월드 좌표 Y |
| `z` | O | 월드 좌표 Z |

```
# 플레이어를 EscapePoint 앞으로 이동
target: "Player(Clone)", x: 10, y: 0, z: 5

# 플레이어를 몬스터 옆으로 이동
target: "Player(Clone)", x: 3, y: 0, z: 3
```

> **주의**: `mcp__mcp-unity__get_hierarchy` 로 대상 오브젝트 이름과 EscapePoint 위치를 먼저 확인할 것.

---

## click_ui — UI 버튼 클릭

**MCP 도구**: `mcp__mcp-unity__click_ui`

Play Mode 중 UI GameObject에 클릭 이벤트를 발생시킨다.
내부적으로 `ExecuteEvents.Execute<IPointerClickHandler>()` 를 사용한다.
Button의 onClick 리스너가 정상 호출된다.

| 파라미터 | 필수 | 설명 |
|----------|:----:|------|
| `target` | O | Button이 붙은 GameObject 이름 |

```
# 제작 버튼 클릭
target: "CraftButton"

# 인벤토리 닫기 버튼 클릭
target: "CloseButton"
```

> **주의**: Play Mode 중 해당 UI가 활성화된 상태여야 한다.

---

## invoke_runtime — 컴포넌트 메서드 호출

**MCP 도구**: `mcp__mcp-unity__invoke_runtime`

Play Mode 중 씬의 컴포넌트 메서드를 Reflection으로 직접 호출한다.
게임 내부 상태 조작(HP 변경, 공격 실행, 인벤토리 아이템 추가 등)에 사용한다.

| 파라미터 | 필수 | 설명 |
|----------|:----:|------|
| `target` | O | 대상 GameObject 이름 |
| `componentType` | O | 컴포넌트 타입 이름 |
| `methodName` | O | 호출할 메서드 이름 |
| `args` | X | 메서드 인자 (쉼표 구분 문자열) |

```
# 플레이어 공격 실행
target: "Player(Clone)", componentType: "PlayerCharacter", methodName: "Attack"

# 플레이어 HP 감소 (데미지 테스트)
target: "Player(Clone)", componentType: "PlayerCharacter", methodName: "AddHP", args: "-30"

# 플레이어 Cold 증가
target: "Player(Clone)", componentType: "PlayerCharacter", methodName: "AddCold", args: "50"

# 몬스터 즉사 (드롭 테이블 테스트)
target: "Test01Monster(Clone)", componentType: "MonsterBase", methodName: "TakeDamage", args: "9999"

# 인벤토리에 아이템 추가 (제작 테스트 준비)
target: "InGameObjectDataWorker", componentType: "InventoryRegistry", methodName: "AddItem", args: "1,10"
```

---

## read_console — 콘솔 로그 확인

**MCP 도구**: `mcp__mcp-unity__read_console`

Unity 콘솔의 로그를 읽어 테스트 결과를 확인한다.
기능 실행 후 예상 로그가 출력됐는지 검증한다.

| 파라미터 | 필수 | 설명 |
|----------|:----:|------|
| `logType` | X | `"error"` / `"warning"` / `"log"` / `"all"` (기본값: `"all"`) |
| `maxCount` | X | 최대 항목 수 (기본값: 50) |

```
# 에러만 확인
logType: "error"

# 최신 로그 20개
logType: "all", maxCount: 20
```

---

## get_hierarchy — 씬 상태 확인

**MCP 도구**: `mcp__mcp-unity__get_hierarchy`

씬의 GameObject 계층 구조를 확인한다.
오브젝트 이름, 컴포넌트 목록, 활성화 상태를 파악할 때 사용한다.

| 파라미터 | 필수 | 설명 |
|----------|:----:|------|
| `maxCount` | X | 최대 노드 수 (기본값: 500) |

```
# 씬 전체 확인 (파라미터 없음)

# 최대 노드 수 제한
maxCount: 100
```

**주요 활용:**
- `invoke_runtime` / `set_position` 실행 전 대상 오브젝트 이름 확인
- EscapePoint 위치 확인
- WorldDropItem 스폰 여부 확인
