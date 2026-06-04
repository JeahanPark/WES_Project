# 아이콘 세트 스타일 가이드 (WES 다크 판타지)

인게임 아이템·UI 아이콘 공통 화풍. AI(Gemini) 생성 시 프롬프트 **프리픽스**로 사용한다.

## 톤 기준
- 다크 판타지, Don't Starve 류 — 어둡고 외로운 분위기
- 검은 굵은 외곽선 (hand-inked black outline)
- 저채도 흙빛 팔레트 (desaturated earthy browns, muted ochre, ash gray)
- 약한 상단 광원 (soft dim top-down light, gentle highlight)
- 단색 어두운 배경 (flat dark muted background, no scene)
- 정사각 256×256, 아이콘 중앙 정렬, 여백 균일

## 프롬프트 프리픽스
```
A single game item icon in dark fantasy hand-drawn style, inspired by Don't Starve:
thick black ink outlines, desaturated earthy palette (muted browns, ash gray, dim ochre),
soft dim top-down lighting with a gentle highlight, lonely grim mood,
flat dark muted solid background, centered composition, 256x256 square, game inventory icon.
Subject:
```

## 사용 규칙
- 같은 세트 아이콘은 같은 Gemini 채팅에서 이어 생성 (스타일 일관성)
- 세션당 15~20장 한도, 초과 시 새 채팅 + 베스트 이미지 체인
- 저장: `Assets/GameResource/Image/ItemIcon/<name>.png`
- 아틀라스: `Icons` 카테고리 편입
