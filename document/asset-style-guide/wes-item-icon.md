# 스타일 가이드 — WES 아이템 아이콘 (G-5 / ★2 기준선)

> **세트명**: wes-item-icon
> **기준선**: director 톤 재정의 지침(2026-06-04). 기존 세트는 기준선 아님 — **전량 갱신 대상**.
> **용도**: Item/ItemIcon 전체 — 인벤토리·제작·퀵슬롯·드롭 공통 의존
> **아틀라스**: `Assets/GameResource/UI/Atlas/Icons.spriteatlas`

---

## 확정 톤 (director 재정의 지침)

| 요소 | 기준 |
|---|---|
| 배경 | **투명 또는 아주 어두운 톤**. 꽉 찬 단색 사각 배경 금지(슬롯 프레임은 UI가 담당) |
| 라인 | Don't Starve식 **굵고 거친 잉크 외곽선**. 손으로 긁은 듯 불균일한 두께. 깔끔한 벡터 라인 금지 |
| 채색 | 저채도 청회·회갈 베이스. 자원=흙·나무·돌의 칙칙한 자연색. **채도 높은 원색 금지**(쨍한 빨강 등) |
| 따뜻함(주황/붉은빛) | **의미 있는 곳에만** — 모닥불·횃불·회복포션처럼 "온기/생명" 의미 아이템에만. 나머지는 차갑게. 2색 정서축(청↔주황)이 아이콘 레벨에서도 작동 |
| 질감 | 약간의 거칠음·낡음. 매끈한 글로시 금지 |
| 형상 | **각 아이템이 무엇인지 형상으로 식별 가능해야**. 심볼/십자가 placeholder 전부 교체 |
| 구도 | 오브젝트 1개 중앙, 정사각 1:1 |

---

## 생성 프롬프트 프리픽스 (Gemini)

### 차가운 아이템 (기본 — 자원·재료·도구 대부분)

```
A single survival item icon, art style of Don't Starve: bold rough hand-drawn black
ink outline with uneven line weight, gritty worn texture, NOT clean vector. Low-saturation
cold earthy palette (dull greys, muddy browns, blue-grey), NO bright primary colors.
Object centered, identifiable shape, 1:1 square, on a transparent or very dark near-black
background (NO filled solid square background). Lonely cold wilderness survival mood.
NO neon, NO gold, NO glossy, NO clean cartoon. The item is:
```

### 따뜻한 아이템 (모닥불·횃불·회복포션 등 "온기/생명" 의미)

```
A single survival item icon, art style of Don't Starve: bold rough hand-drawn black
ink outline with uneven line weight, gritty worn texture, NOT clean vector. Low-saturation
earthy palette but with a meaningful WARM glow (orange/amber firelight or life-warmth),
the warmth scarce and significant against the cold. Object centered, identifiable shape,
1:1 square, transparent or very dark near-black background (NO filled solid square).
Lonely wilderness survival mood. NO neon, NO gold trim, NO glossy. The item is:
```

> 뒤에 형상 설명만 붙임. 차가운 예: `... The item is: a bundle of rough firewood logs.`
> 따뜻한 예: `... The item is: a healing potion in a worn vial glowing warm red.`

---

## 따뜻함/차가움 분류 (2색 정서축)

| 따뜻함(주황·붉은빛) | 차가움(기본) |
|---|---|
| campfire, torch, potion_hp(회복) | wood, stone, herb, leather, bone, ironore, ironsword, sword, shield, leatherarmor, bandage, potion_cold, waterskin, rope, stone_chunk |

> potion_cold는 추위 관련이라 **청색** 계열 발광(따뜻함 아님).

---

## 세션 운용

- 같은 세트(아이콘)는 **한 채팅**에서 이어 생성 → 스타일 일관성.
- 15~20장마다 새 채팅 체인: 직전 베스트 + 본 프리픽스 투입 후 "이 스타일로 이어서".
- 첫 메시지에 본 프리픽스로 스타일 고정.

---

## import

다운로드 PNG → `AiTextureImportSetup.ImportAndPack(src, "Assets/GameResource/Image/ItemIcon", "<name>_icon", "Icons")`
또는 폴더 일괄: `WES/AI Texture/Normalize ItemIcon Sprites And Pack Icons`.

---

## 생성 우선순위 (director 지정)

1. **자원 6종 먼저**: wood, stone, herb, leather, bone, ironore (인벤·제작·드롭·퀵슬롯 전부 의존)
2. 소비/장비/건물: potion_hp, potion_cold, bandage / sword, ironsword, shield, leatherarmor / campfire, torch, waterskin, rope, stone_chunk
3. blueprint류: "낡은 도면 종이" 컨셉 유지, **잉크 라인 강조 + 종이 더 빛바래게(누렇게·얼룩)**. 갱신만.

## 1차 샘플 (톤 컨펌용 — director 지정)

wood(차가움) + potion_hp(따뜻함 케이스) + ironore(차가움). 3종 뽑아 컨펌 후 전량 진행.
