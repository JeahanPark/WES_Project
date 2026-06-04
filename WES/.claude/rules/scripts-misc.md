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
- `AiTextureImportSetup.cs` — AI 생성 이미지 import + 워터마크 크롭(CropInsetPng/MaskCornerWatermarkPng) + Sprite 정규화 + 카테고리 아틀라스 편입
- `UiResourceImportSetup.cs` / `UiFrameImportSetup.cs` / `UiGaugeImportSetup.cs` / `UiBackgroundImportSetup.cs` / `UiResourceApplySetup.cs` — UI 자산 카테고리별 import·프리팹 적용 일괄 메뉴
- `CoreTensionTextureSetup.cs` — 코어텐션 오버레이 텍스처 procedural 생성
- `CoreTensionOverlayBuildSetup.cs` — 씬 코어텐션 오버레이 GameObject 생성·와이어링

## Worker/ — 기능 워커 (일부)
- `CoreTensionOverlayWorker.cs` — 풀스크린 코어텐션 오버레이 구동(추위 3단계·HP 적색비네팅·사망 암전 PlayDeathFade·낮밤 틴트·안개 스크롤)

## 루트 파일
- `GameEnum.cs` — 게임 전역 Enum
- `GameDebug.cs` — 디버그 유틸리티
- `Util.cs` — 공통 유틸리티
