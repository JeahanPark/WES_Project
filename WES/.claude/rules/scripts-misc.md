---
paths:
  - "Assets/Scripts/Collider/**"
  - "Assets/Scripts/Component/**"
  - "Assets/Scripts/Input/**"
  - "Assets/Scripts/Editor/**"
---

# 기타 Scripts 폴더

## Collider/ — 충돌 처리
- `BaseColliderObject.cs` — 충돌 오브젝트 베이스
- `InGameColliderWorker.cs` — 충돌 워커

## Component/ — GameObject 단일 기능 컴포넌트
- `MonsterSpawnArea.cs` — 몬스터 스폰 영역

## Input/ — InputSystem
- `InputSystem_Actions.cs` — InputSystem C# wrapper (자동 생성)

## Editor/ — 에디터 전용
- `BaseScrollEditor.cs` — 스크롤 커스텀 에디터
- `InfoConvertEditor.cs` — CSV → Info 변환 에디터 도구

## 루트 파일
- `GameEnum.cs` — 게임 전역 Enum
- `GameDebug.cs` — 디버그 유틸리티
- `Util.cs` — 공통 유틸리티
