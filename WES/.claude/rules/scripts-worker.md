---
paths:
  - "Assets/Scripts/Worker/**"
---

# Worker 스크립트 구조

씬 내 기능별 워커. 특정 도메인/기능 담당, 씬 전환 시 파괴됨.

- `InGameCameraWorker.cs` — 카메라 제어
- `InGamePlayWorker.cs` — 플레이 로직
- `InGameSpawnWorker.cs` — 스폰 처리
- `InGameAreaWorker.cs` — 월드 영역 관리
- `InGameWorldUIWorker.cs` — 월드 UI 관리
- `BuildingPlacementWorker.cs` — 건물 배치
