---
paths:
  - "Assets/GameResource/**"
---

# GameResource 리소스 구조

게임에 사용되는 모든 리소스. Addressable로 런타임 로드.

```
GameResource/
├── UI/
│   ├── Popup/    — CraftPopup, InventoryPopup, LobbyPopup, LobbyRoomPopup, LoginPopup
│   ├── HUD/      — CraftHUDTab, PlayerStatusHUD, QuickSlotHUD
│   ├── WorldUI/  — CharacterWorldUI
│   └── TestUI/   — 테스트용
├── Character/    — 캐릭터 모델/애니메이션 (Man, Sample, Test01Monster)
├── Item/         — 아이템 프리팹
├── Image/ItemIcon/ — 아이콘 이미지 (campfire, stone, sword, wood)
├── Test/         — 테스트용 리소스
└── Noto_Sans_KR/ — 한국어 폰트
```

UI 프리팹은 `Scripts/UI/` 하위의 동명 스크립트와 쌍을 이룸.
