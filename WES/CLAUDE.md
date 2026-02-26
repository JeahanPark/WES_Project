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

## MCP Rules
- Unity 관련 작업(프리팹, 컴포넌트, Inspector 참조 등)은 MCP 도구를 우선 사용한다.
- 사용 가능한 도구: `echo` / `generate_ui_with_gpt` / `manage_components` / `set_reference`
- 도구 사용법은 반드시 [MCP/README.md](MCP/README.md)를 참고한다.

---

**Note:** These are baseline rules with standard priority. They may be adjusted or overridden based on specific project requirements or context.
