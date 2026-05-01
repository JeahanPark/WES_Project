## MCP Rules
- Unity 관련 작업(프리팹, 컴포넌트, Inspector 참조 등)은 MCP 도구를 우선 사용한다.
- 사용 가능한 도구: `echo` / `generate_ui_with_gpt` / `manage_components` / `set_reference` / `instantiate_prefab` / `add_gameobject` / `connect_button` / `refresh_assets`
- 도구 사용법은 반드시 [Assets/MCP_Unity_Plugin/README.md](Assets/MCP_Unity_Plugin/README.md)를 참고한다.
- **MCP 한계 리스트업**: MCP 호출 시 불가능한 상황이 발생하면, 해당 작업이 끝난 후 불가능했던 항목을 리스트업한다.
- **MCP 기능 수정/추가 전 토큰 체크**: MCP 기능을 수정하거나 추가하기 전에 토큰 사용량을 체크하고 사용자에게 보고한다.