---
paths:
  - "Assets/Scripts/Manager/**"
---

# Manager 스크립트 구조

글로벌 싱글톤 매니저들. `MonoSingleton` 상속, `DontDestroyOnLoad`, `Managers` 클래스를 통해 접근.

- `Managers.cs` — 매니저 통합 접근 클래스
- `InputManager.cs` — 입력 처리
- `PopupManager.cs` — 팝업 UI 관리
- `ResourceManager.cs` — Addressable 리소스 로드
- `ChatManager.cs` — 채팅
- `GameNetworkManager.cs` — 네트워크
- `GameSceneManager.cs` — 씬 전환
- `TestManager.cs` — `#if UNITY_EDITOR` 전용, QA 테스트용
