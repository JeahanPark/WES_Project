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

---

**Note:** These are baseline rules with standard priority. They may be adjusted or overridden based on specific project requirements or context.
