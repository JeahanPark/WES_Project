---
paths:
  - "Assets/GameResource/**"
---

# GameResource 리소스 구조

게임에 사용되는 모든 리소스. Addressable로 런타임 로드.

```
GameResource/
├── UI/
│   ├── Popup/      — CraftPopup, InventoryPopup, LobbyPopup, LobbyRoomPopup, LoginPopup, ResultPopup
│   ├── HUD/        — CraftHUDTab, PlayerStatusHUD, QuickSlotHUD, BlueprintToast
│   ├── WorldUI/    — CharacterWorldUI, DamageNumberWorldUI
│   ├── Frame/      — 9-slice 버튼·패널·슬롯 프레임 sprite (btn_frame_*, panel_frame, slot_frame)
│   ├── Gauge/      — 게이지 스킨 (gauge_frame_empty, gauge_fill_hp/cold/stamina)
│   ├── Background/ — 화면 배경·로고 (bg_title/lobby/clear_*, logo_main)
│   ├── CoreTension/— 풀스크린 오버레이 텍스처 (cold_overlay_1~3, vignette_red, death_overlay, daynight_gradient, ambient_fog)
│   ├── Atlas/      — Sprite Atlas (Icons, Ui)
│   └── TestUI/     — 테스트용
├── Character/    — 캐릭터 모델/애니메이션 (Man, Sample, Test01Monster)
├── Item/         — 아이템 프리팹
├── Image/ItemIcon/ — 아이콘 이미지 (AI 생성 다크 톤 21종: campfire, stone, wood, herb, ...)
├── Shader/       — 셰이더 (NightDarknessOverlay 등)
├── Test/         — 테스트용 리소스
└── Noto_Sans_KR/ — 한국어 폰트
```

UI 프리팹은 `Scripts/UI/` 하위의 동명 스크립트와 쌍을 이룸.
아이콘·UI 텍스처는 AI(Gemini) 생성 → 8% 워터마크 크롭 → 카테고리 Sprite Atlas 편입. 코어텐션 오버레이는 `CoreTensionOverlayWorker`가 구동.
