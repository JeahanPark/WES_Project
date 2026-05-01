---
paths:
  - "Assets/Scripts/UI/**"
---

# UI 스크립트 구조

UI는 4개 카테고리로 분류. 각 스크립트는 `GameResource/UI/` 하위의 동명 프리팹과 쌍을 이룸.

## Popup/ — 팝업 UI
- `BasePopup.cs` — 팝업 베이스 클래스
- `LoginPopup.cs`, `LobbyPopup.cs` — 개별 팝업

## HUD/ — 인게임 HUD
- `InGameHUDWorker.cs` — HUD 전체 관리
- `PlayerStatusHUD.cs`, `StatBar.cs` — 플레이어 상태
- `QuickSlotHUD.cs`, `QuickSlotCell.cs` — 퀵슬롯
- `CraftHUDTab.cs` — 크래프트 탭

## WorldUI/ — 월드 공간 UI
- `BaseWorldUI.cs` — 월드 UI 베이스
- `CharacterWorldUI.cs` — 캐릭터 머리 위 UI

## Scroll/ — 스크롤 뷰 시스템
- `BaseScroll.cs`, `BaseScrollCell.cs` — 재사용 스크롤 베이스
