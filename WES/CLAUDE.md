# WES Project - Claude Instructions

# PROJECT CODING RULES (MUST FOLLOW)

## Core Rules
- All member variables MUST be `private` by default.
- External access MUST be provided ONLY via `public` methods.
- Public fields are FORBIDDEN.

## Naming Rules
- Member variables MUST use the `m_` prefix.
- Parameters MUST use the `_` prefix.
- Coroutine method names MUST start with `Co`.
- UI button mapping/handler methods MUST start with `OnClick`.

## Inspector / Serialization
- Any field exposed in the Inspector MUST be declared as:
  - `[SerializeField] private <type> m_Name;`
- DO NOT use `public` fields for Inspector exposure.
- `[SerializeField] static` is NOT allowed.

## Constant Rules
- Compile-time constants MUST be declared using `const`.
- Constants MAY be `public`.
- Constant names MUST use `UPPER_SNAKE_CASE`.
- Example:
  - `public const int MAX_PLAYER_COUNT = 4;`

## Static Rules
- Static fields are FORBIDDEN by default.
- Static fields are allowed ONLY for:
  - `const` values
  - `private static readonly` configuration values
  - Explicit global managers (singleton-style)
- ALL static fields MUST be `private`.
- Direct public access to static fields is FORBIDDEN.
- Use `public static` properties or methods for access.

## Coroutine Rules
- ALL coroutine methods MUST start with the prefix `Co`.
- Any method that returns `IEnumerator` MUST be treated as a coroutine.
- `IEnumerator` methods WITHOUT the `Co` prefix are FORBIDDEN.

## Architecture Rules
- **Manager**: Accessible from any scene (global singleton, inherits `MonoSingleton`)
  - Accessed globally through the `Managers` class
  - Persists across scene transitions (`DontDestroyOnLoad`)
  - Examples: `InputManager`, `PopupManager`, `GameManager`
- **Controller**: Manages a single scene (exists only within the scene)
  - Manages overall scene flow and logic
  - Destroyed on scene transition
  - Examples: `InGameController`, `LobbyController`
- **Worker**: Manages a specific feature (handles individual functionality within a scene)
  - Uses regular `MonoBehaviour`
  - Focuses on a specific domain or feature
  - Destroyed on scene transition
  - Examples: `CameraWorker`, `UIWorker`
- **Component**: Handles a specific functionality attached to a game object
  - Uses regular `MonoBehaviour`
  - Attached to and manages a specific aspect of a single GameObject
  - Focused on a single responsibility (animation, input, physics, etc.)
  - Can be reused across different object types
  - Destroyed with the GameObject it's attached to
  - Examples: `CharacterAnimationComponent`, `HealthComponent`, `InteractionComponent`

## Class Layout Order (MUST KEEP THIS ORDER)
1. `const` fields
2. `private static readonly` fields
3. `[SerializeField] private` member variables
4. `public` member variables (ONLY if explicitly allowed)
5. `private` member variables
6. Unity lifecycle methods (Awake, OnEnable, Start, Update, LateUpdate, OnDisable, OnDestroy, etc.)
7. `public` methods
8. `private` methods
9. `OnClick*` methods

## Button Binding Rules
- 버튼 바인딩은 반드시 `[SerializeField] private Button m_XxxButton;` 으로 참조를 Inspector에서 연결한다.
- 실제 메서드 바인딩은 `Awake`에서 `m_XxxButton.onClick.AddListener(OnClickXxx);` 로 코드에서 처리한다.
- Inspector의 Button onClick 이벤트에 직접 메서드를 Persistent Listener로 등록하는 방식은 사용하지 않는다.

## MCP Rules
- Unity 관련 작업(프리팹, 컴포넌트, Inspector 참조 등)은 MCP 도구를 우선 사용한다.
- 사용 가능한 도구:
  - **에디터**: `u_editor_component` / `u_editor_gameobject` / `u_editor_set_transform` / `u_editor_query` / `u_editor_reference` / `u_editor_prefab` / `u_editor_scene` / `u_editor_asset` / `u_editor_tag` / `u_editor_layer`
  - **플레이모드**: `u_play_control` / `u_play_set_transform` / `u_play_click` / `u_play_invoke`
  - **공통**: `u_console` / `u_screenshot`
  - **일반**: `echo` / `generate_ui_with_gpt`
- 도구 사용법은 반드시 [Assets/MCP_Unity_Plugin/README.md](Assets/MCP_Unity_Plugin/README.md)를 참고한다.
- 에셋(스크립트, CSV, 프리팹 등)을 추가/수정/삭제한 경우 작업 완료 후 반드시 `u_editor_asset(action: refresh)` 도구를 호출하여 Unity 에디터에 변경 사항을 반영한다.
- **MCP 플러그인 수정 시**: MCP_Unity_Plugin 코드를 수정해야 할 경우, 반드시 원본 저장소(`C:\GitFork\MCP_Unity\MCP_Unity_Plugin`)에서 먼저 수정한 뒤 프로젝트(`Assets/MCP_Unity_Plugin/`)로 복사한다. 프로젝트 내 파일을 직접 수정하지 않는다.
- **MCP 서버 재빌드/재시작**: MCP 서버를 재빌드하거나 재시작해야 할 경우, 배치 파일 `C:\GitFork\MCP_Unity\MCP\MCP\MCP\stop_and_rebuild.bat`를 실행한다.

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
