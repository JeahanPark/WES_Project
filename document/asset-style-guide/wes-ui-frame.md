# 스타일 가이드 — WES UI 프레임/버튼 룩 (I-3 / ★1 기준점)

> **세트명**: wes-ui-frame
> **용도**: 전 단계 공유 — 버튼·슬롯·창 테두리·패널 프레임. 9-slice 전제.
> **아틀라스**: `Assets/GameResource/UI/Atlas/Ui.spriteatlas` (신규)

---

## 확정 톤

| 요소 | 기준 |
|---|---|
| 모티프 | 거친 나무판 + 밧줄 묶음 + 녹슨 금속 못/경첩. 낡고 닳음 |
| 팔레트 | 저채도 흙빛(dark warm brown #2a241d ~ #4a3f30), 회갈색. 금속은 바랜 철색(녹) |
| 라인 | 잉크 톤 외곽 + 나뭇결·못 디테일. Don't Starve 판각 느낌 |
| 상호작용 신호 | 호버/선택 = 가장자리에 주황 불빛(#d98a3d)이 스미는 느낌. 비활성 = 탈색·어둡게 |
| 금지 | 네온, 골드 테두리, 글래스/SF, 미니멀 플랫, 둥근 파스텔 |

---

## 9-slice 원칙

- 테두리만 그리고 중앙은 늘어나도 자연스럽게(나뭇결 반복 OK).
- 생성은 정사각 큰 타일(예 512²)로, 중앙은 균질, 모서리에 못·밧줄 매듭 배치.
- import 후 sprite border 9-slice 값 설정(좌우상하 동일 px, 모서리 디테일 보존되게).
- 개별 버튼 크기마다 따로 생성하지 않는다 — 한 프레임을 9-slice로 전 크기 커버.

---

## 생성 프롬프트 프리픽스 (Gemini)

```
A 9-slice UI panel/button frame texture in the art style of Don't Starve:
weathered wooden planks bound with frayed rope and rusty metal nails/hinges,
thick ink outlines, hand-drawn wood grain. Low-saturation earthy palette
(dark warm brown #2a241d to #4a3f30, faded rusted iron). Gritty worn survival mood.
Square tile, the border detail near edges, the center area uniform and tileable.
NO neon, NO gold trim, NO glass/sci-fi, NO flat minimal, NO bright colors.
The element is:
```

> 뒤에 용도만 붙임. 예: `... The element is: a rectangular button frame, idle state.`
> 상태별(idle / hover with warm orange glow / disabled desaturated)을 한 채팅에서 이어 생성.

---

## 생성 목록 (1차)

| 자산명 | 용도 | 9-slice |
|---|---|---|
| btn_frame_idle | 버튼 기본 | O |
| btn_frame_hover | 버튼 호버(주황 불빛) | O |
| btn_frame_disabled | 버튼 비활성(탈색) | O |
| panel_frame | 창/팝업 테두리 | O |
| slot_frame | 인벤/퀵슬롯 칸 | O |

---

## import

다운로드 PNG → `AiTextureImportSetup.ImportAndPack(src, "Assets/GameResource/UI/Frame", "<name>", "Ui")`
→ 이후 9-slice border는 `UiResourceImportSetup`의 border 설정 메뉴로 일괄 적용.
