---
name: InfoManager
category: Manager
parent: "[[Singleton]]"
file_path: WES/Assets/Scripts/Info/InfoManager.cs
role: CSV 기획 데이터(Info) 통합 매니저 — 로드·조회. Managers.Info
status: Active
signals: []
---

# InfoManager

CSV 기획 데이터(BuildingInfo, CraftInfo, ItemInfo 등)를 로드·보관하는 통합 매니저. `Managers.Info`. `partial class` — 자동 생성 로더(`InfoLoader.cs`)와 수동 파일(`InfoManager.cs`)로 분리.

## 책임 영역

- **데이터 로드**: `LoadAllInfo()`(자동생성) → 각 `*InfoList` 채움.
- **멱등 로드 보장** `LoadAllInfoOnce()` (+ `m_IsInfoLoaded`/`IsInfoLoaded`): 진입 경로(MPPM Host / TestMode / 일반)와 무관하게 **1회만 로드**. 중복 0.
- 조회 헬퍼: `GetCraftInfosByCategory` / `GetMaterialsByCraftId` / `GetConditionsByCraftId`.
- **도면 해금(②)**: `BlueprintInfoList`(BlueprintInfo.csv) + `GetBlueprintByCraftId` / `GetBlueprintByItemId` / `IsBlueprintLockedCraft(craftId)`. [[RecipeUnlockRegistry]]가 잠금 판정에 사용.

## 변경 이력

- 2026-06-03: `LoadAllInfoOnce()` 신규. **버그 수정** — MPPM Host 부트스트랩(`MppmBootstrapWorker.RunAsHostAsync`)이 `LoadAllInfo`를 안 불러 서버 `BuildingInfoList`가 비어 `BuildingInfo not found:1`(모닥불 스폰 실패) 발생하던 문제. 이제 부트스트랩·TestMode 양쪽이 `LoadAllInfoOnce`를 호출 → 경로 무관 1회 로드.

## 관련

- 부모: [[Singleton]]
- 호출처: [[MppmBootstrapWorker]] (Host 진입) · [[InGameController]] (CoInitializeTestMode) · [[IntroController]]
- 데이터: `Assets/CSVInfo/*.csv` (`InfoLoader`가 로드)
