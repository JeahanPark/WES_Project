# Dev-QA Workflow Design

## Overview
기능 개발 -> 런타임 QA -> 자동 판정 -> 문제 시 재개발 워크플로우를 구축한다.
MCP 도구를 활용하여 Unity 플레이모드에서 실제 기능을 테스트한다.

## 1. TestManager

### 구조
- `MonoSingleton<TestManager>` 상속
- `Managers.Test`로 접근
- `#if UNITY_EDITOR`로 전체 클래스를 감싸서 릴리즈 빌드 제외
- `DontDestroyOnLoad`로 씬 전환에도 유지

### 원칙
- **테스트 전용 로직 금지** — 기존 public 메서드를 조합만 한다
- 시나리오별 public 메서드 제공 (예: `TestSpawnAndKillMonster()`)
- `invoke_runtime`으로 호출

### 파일 위치
- `Assets/Scripts/Manager/TestManager.cs`

## 2. 워크플로우 사이클

```
개발 작업 (코드 + MCP)
    |
refresh_assets -> 컴파일 확인
    |
read_console -> 에러 체크
    |
play_mode_control(enter) -> 플레이모드 진입
    |
invoke_runtime -> TestManager 시나리오 실행
    |
gameobject_find / read_console / screenshot -> 결과 검증
    |
play_mode_control(exit) -> 플레이모드 종료
    |
판정
```

## 3. 판정 규칙

| 상황 | 대응 |
|------|------|
| 코드 에러, 런타임 에러 | 자동 수정 후 재시도 |
| MCP 도구 부족 | `invoke_runtime`으로 우회 시도 |
| 기획적으로 시나리오 진행 불가 | **중단, 사용자에게 보고** |
| MCP 자체 수정이 불가피 | **세션 양도** |

## 4. 세션 양도 시스템

MCP 수정 등으로 세션 재시작이 필요할 때:

### A세션 (현재)
1. 인수인계 파일(`docs/handoff/current-task.md`)에 기록
   - 목표 (무슨 기능)
   - 현재 단계
   - 중단 사유
   - 다음에 해야 할 일
2. MCP 코드 수정 + `stop_and_rebuild.bat` 실행
3. 사용자에게 "새 세션에서 `/dev-qa` 실행" 안내

### B세션 (새 세션)
1. `/dev-qa` 실행 시 인수인계 파일 감지
2. 이전 작업 이어받아 사이클 재개
3. 완료 후 인수인계 파일 삭제

## 5. 서브에이전트 활용 기준

- 워크플로우 사이클 자체는 순차 실행 (앞 단계 결과가 다음에 영향)
- MCP 도구는 Unity Editor 하나에 연결 — 동시 호출 시 충돌 가능
- **개발 단계 안에서** 독립적인 코드 작업이 여러 개일 때만 서브에이전트 사용

## 6. 관리 방식

- **CLAUDE.md** — TestManager 존재, 기본 원칙
- **커스텀 스킬** (`/dev-qa`) — 상세 워크플로우 절차 + 인수인계 파일 체크 로직
