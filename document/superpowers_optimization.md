# Superpowers 스킬 최적화 계획

플러그인 경로: `C:\Users\cgq02\.claude\plugins\superpowers\skills\`

현재 14개 스킬 중 9개를 제거 대상으로 분류.

## 유지 (5개)

| 스킬 | 설명 |
|------|------|
| `using-superpowers` | 핵심 - 스킬 시스템 자체 |
| `brainstorming` | 기능 설계 전 브레인스토밍 |
| `writing-plans` | 복잡한 작업의 상세 계획 작성 |
| `systematic-debugging` | 체계적 디버깅 프로세스 |
| `verification-before-completion` | 작업 완료 전 검증 |

## 제거 대상 (9개)

### 1. `test-driven-development` (370줄)
- 테스트 먼저 작성 -> 실패 확인 -> 최소 코드 작성 -> 리팩토링 (Red-Green-Refactor)
- **제거 이유**: Unity C# 프로젝트에서 자동 테스트 미사용. MCP + 플레이모드 QA(`dev-qa` 스킬)로 대체

### 2. `using-git-worktrees` (219줄)
- 별도 브랜치를 격리된 디렉토리에 체크아웃해서 동시 작업
- **제거 이유**: main 브랜치 직접 작업 중. 브랜치 전략 미사용

### 3. `finishing-a-development-branch` (201줄)
- 작업 완료 후 merge/PR/discard 4가지 옵션 제시 + worktree 정리
- **제거 이유**: worktree, 브랜치 전략 미사용. `commit-push` 스킬로 대체

### 4. `receiving-code-review` (213줄)
- 코드 리뷰 피드백 받을 때 맹목적 수용 금지, 기술적 검증 후 대응
- **제거 이유**: 솔로 개발. 외부 리뷰어 없음

### 5. `requesting-code-review` (105줄)
- 서브에이전트를 코드 리뷰어로 파견해서 품질 검증
- **제거 이유**: 솔로 개발. PR 기반 리뷰 프로세스 없음

### 6. `executing-plans` (71줄)
- 작성된 계획 파일을 읽고 순차적으로 태스크 실행 (서브에이전트 없는 환경용)
- **제거 이유**: `writing-plans`로 계획 수립 후 같은 세션에서 바로 실행하면 됨. 별도 세션 핸드오프 워크플로우 미사용
- **참고**: `writing-plans`(계획 작성)와 `executing-plans`(계획 실행)는 쌍으로 동작하지만, 같은 세션에서 바로 작업하는 방식에서는 executing만 불필요

### 7. `subagent-driven-development` (277줄)
- 태스크마다 서브에이전트 파견 -> 구현 -> 스펙 리뷰 -> 코드 리뷰 2단계 검증
- **제거 이유**: 풀 파이프라인이 과한 프로세스. Unity 프로젝트에서 서브에이전트 다수 운용 시 MCP 충돌 위험

### 8. `dispatching-parallel-agents` (180줄)
- 독립적인 문제 여러 개를 병렬 에이전트로 동시 처리
- **제거 이유**: Claude Code 기본 Agent 도구로 이미 가능. 별도 스킬 불필요

### 9. `writing-skills` (656줄)
- 스킬을 TDD 방식으로 작성하는 메타 스킬
- **제거 이유**: 스킬 작성 빈도 매우 낮음. 이 정도 엄격한 프로세스 불필요

## 예상 효과
- 총 약 2,292줄 제거
- 스킬 목록 노이즈 9개 감소
- 매 메시지마다 스킬 판단 부하 감소

## 상태
- 2026-04-16: 분석 완료, 제거 보류 (기록만 해둠)
