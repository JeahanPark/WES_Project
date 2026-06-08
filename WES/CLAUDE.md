# WES Project - Claude Instructions

야생 생존 탈출(WES) — Unity 6 / Netcode for GameObjects 게임. 1인 개발, Steam 출시 목표.

## 컨텍스트 분배 안내

상세 규칙은 작업 영역별로 분리되어 있다:

- **코딩 규칙 / Unity 아키텍처 / 클래스 레이아웃 / Button Binding**:
  → [.claude/agents/client.md](.claude/agents/client.md) 의 "WES 코딩 규칙" 섹션
- **게임 디자인 / 콘텐츠 / 톤 / 코어 비전**:
  → [.claude/agents/director.md](.claude/agents/director.md), [document/WES_GDD.md](document/WES_GDD.md), [document/design/CORE_자원투자_트레이드오프_설계.md](document/design/CORE_자원투자_트레이드오프_설계.md)
- **팀 에이전트 운영 (최대 7인 풀: director/client/designer/qa/sound/story/level-design — 주제별 선택 spawn)**:
  → [.claude/agents/TEAM_PROCESS.md](.claude/agents/TEAM_PROCESS.md) — Phase 표준 절차 (부트스트랩·기획 합의·슬라이스 구현·리소스 명세서 갱신·통합 QA·마무리). 매번 전원 spawn 금지, 주제에 필요한 멤버만.
- **리소스 인벤토리 정책 (디자이너 에이전트가 참조)**:
  → [../document/RESOURCE_INVENTORY.md](../document/RESOURCE_INVENTORY.md) — 폴더 정책·외부 에셋 출처·자산 우선순위 트리
- **문서 양식 (스케줄·작업·에이전트 문서를 쓸 때)**:
  → [../document/문서양식.md](../document/문서양식.md) — 3종 frontmatter·위치·표준 섹션·컨벤션. 새 문서는 이 양식으로 시작.

본 파일에는 메인 세션이 직접 다루는 워크플로우 규칙(리소스 / MCP / Dev-QA)만 유지한다.

---

## Resource Rules
- 게임에 사용되는 모든 리소스(프리팹, 이미지, 애니메이션 등)는 `Assets/GameResource/` 하위에 위치해야 한다.
- 폴더 구조: `GameResource/UI/Popup/`, `GameResource/UI/HUD/`, `GameResource/Character/`, `GameResource/Item/`, `GameResource/Image/` 등
- Addressable로 런타임 로드하는 에셋은 반드시 Addressable Group에 등록한다.
- Addressable Address는 에셋 파일명(확장자 제외)을 사용한다 (예: `wood_icon`, `CraftPopup`).

## MCP Rules
- Unity 관련 작업(프리팹, 컴포넌트, Inspector 참조 등)은 MCP 도구를 우선 사용한다.
- 사용 가능한 도구:
  - **에디터**: `u_editor_component` / `u_editor_gameobject` / `u_set_transform` / `u_editor_prefab` / `u_editor_scene` / `u_editor_asset` / `u_editor_tag_layer`
  - **플레이모드**: `u_play` / `u_set_transform` (mode='play')
  - **공통**: `u_console` / `u_screenshot`
  - **일반**: `generate_ui_with_gpt`
- 도구 사용법은 반드시 [Assets/MCP_Unity_Plugin/README.md](Assets/MCP_Unity_Plugin/README.md)를 참고한다.
- 에셋(스크립트, CSV, 프리팹 등)을 추가/수정/삭제한 경우 작업 완료 후 반드시 `u_editor_asset(action: refresh)` 도구를 호출하여 Unity 에디터에 변경 사항을 반영한다.
- **MCP 플러그인 수정 시**: MCP_Unity_Plugin 코드를 수정해야 할 경우, 반드시 원본 저장소(`C:\GitFork\MCP_Unity\MCP_Unity_Plugin`)에서 먼저 수정한 뒤 프로젝트(`Assets/MCP_Unity_Plugin/`)로 복사한다. 프로젝트 내 파일을 직접 수정하지 않는다.
- **MCP 서버 재빌드/재시작**: MCP 서버를 재빌드하거나 재시작해야 할 경우, 배치 파일 `C:\GitFork\MCP_Unity\MCP\MCP\MCP\stop_and_rebuild.bat`를 실행한다.

## Auto-Doc Wiki Rules (Karpathy LLM Wiki 패턴)

WES는 `document/auto/` 를 에이전트가 유지하는 자동 문서 wiki로 운용한다. 상세 스펙: [../document/obsidian_agent_plan.md](../document/obsidian_agent_plan.md).

### Wiki-First 원칙
- **시스템·기능에 대한 질문/작업을 받으면 코드 탐색 전에 vault부터 확인한다.**
- 진입점은 항상 [../document/auto/index.md](../document/auto/index.md). 거기서 관련 catalog 페이지로 들어간다.
- vault에 충분한 정보가 있으면 코드 탐색 없이 vault만으로 답한다.
- vault에 없거나 부족하면 코드를 탐색하고, 결과를 vault에 반영한다.

### Trust-but-Verify 원칙
- **vault는 빠른 참조용이지 진실의 원천이 아니다. 진실은 항상 코드에 있다.**
- vault 내용으로 **코드를 수정**할 때는 반드시 실제 코드를 읽어 재검증한다.
- vault와 코드가 다르면 → 코드가 맞다. vault를 수정한다.

### 매 작업 사이클
1. **작업 시작 시**: `document/auto/index.md` 를 읽어 현재 vault 상태 파악
2. **작업 중**: 새로 알게 된 사실은 즉시 관련 catalog 페이지에 반영 (frontmatter 갱신, wiki link 추가)
3. **작업 종료 시**:
   - 변경된 클래스의 `catalog/Class/*.md` upsert
   - `reports/YYYY-MM-DD-제목.md` 작업 리포트 생성
   - `log.md` 에 한 줄 append
   - 팀 컨텍스트가 있으면 해당 Discord thread에 요약 push (별도 스레드 생성 금지)

### 작업 어휘 (Karpathy 패턴)
- **Ingest** — 새 코드/소스가 들어옴 → catalog 일괄 갱신
- **Query** — vault에서 답 찾기. 좋은 답은 페이지로 환원
- **Lint** — 모순/고아 페이지/누락 참조 주기 점검 (수동, 월 1회 정도)

## Dev-QA Workflow Rules
- 기능 개발 시 **개발 → QA → 판정** 사이클을 따른다.
- QA는 MCP 도구(`u_play_control`, `u_play_invoke`, `u_console`, `u_screenshot`, `u_editor_query`)를 사용하여 플레이모드에서 실제 검증한다.
- `TestManager`(`Managers.Test`)는 `#if UNITY_EDITOR` 전용 MonoSingleton으로, 테스트 시나리오를 관리한다.
- **TestManager 원칙**: 테스트 전용 로직 금지. 기존 public 메서드를 조합만 한다.
- **자동 수정**: 코드 에러, 런타임 에러, MCP 도구 부족(`u_play_invoke` 우회) 등 기술적 문제는 자동으로 수정 후 재시도한다.
- **중단 조건**: 기획적으로 시나리오 진행이 불가능한 경우에만 사용자에게 보고하고 중단한다.
- **세션 양도**: MCP 자체 수정이 불가피할 경우, `/rename`으로 세션 이름을 붙이고 사용자에게 재빌드 후 `/resume`으로 복귀하도록 안내한다.
- **서브에이전트**: 워크플로우 사이클은 순차 실행. 개발 단계 내 독립적인 코드 작업이 여러 개일 때만 서브에이전트를 사용한다.

---

**Note:** These are baseline rules with standard priority. They may be adjusted or overridden based on specific project requirements or context.
