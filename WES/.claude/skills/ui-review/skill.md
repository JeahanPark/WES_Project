---
name: ui-review
description: Use when reviewing UI visual quality (Canvas, HUD, Popup). Captures screenshots, checks for layout/alignment/overflow issues, and reports findings. Does not modify code or assets.
---

# UI Review

UI(Canvas, HUD, Popup)의 시각적 품질을 검수하는 스킬.
**자동 검사(코드)** + **시각 검수(스크린샷+수치)** 2단계로 분리. 수정 금지, 리포트 전용.

## 1. 호출 방식

- `/ui-review` — 현재 씬에서 열 수 있는 UI 전체
- `/ui-review <UI명>` — 특정 UI만 (예: `/ui-review CraftPopup`)

## 2. 검수 모드

### 1차 검수 (리소스 구현 후)
- 모드: 에디터 또는 플레이
- 데이터: 기본/더미 상태
- 중점: 레이아웃, 정렬, 크기, 겹침, 일관성

### 2차 검수 (QA 단계)
- 모드: 플레이 모드 필수
- 데이터: 실제 게임 데이터
- 중점: 극단값, 상태별, 전환, 해상도 대응

## 3. 핵심 원칙 — 스크린샷 단독 검수 금지

**왜 필요한가**: 게임 뷰 스크린샷(통상 480~720px)에서는 픽셀 단위 왜곡(아이콘 비율 깨짐, 행 높이 미세 어긋남)이 보이지 않음. 시각만으로 통과 처리하면 실제 결과는 깨져있는 경우 발생.

**규칙**:
1. **시각 의심 → 수치 조회 의무**: 비율·크기·정렬이 의심되면 반드시 `RectTransform.rect`, `TMP_Text.preferredWidth/Height`, `Image.sprite.rect` 등을 조회해 숫자로 검증
2. **반복 인스턴스(Instantiate된 행/셀)는 첫 번째 클론의 실제 rect 조회**: 템플릿의 sizeDelta가 아니라, 런타임에 LayoutGroup이 적용된 실제 크기를 본다
3. **의심 가는 요소는 zoom 캡처**: `u_editor_sceneview`로 해당 영역만 확대해서 다시 캡처

## 4. 체크리스트

### A. 자동 검사 (코드 우선, 스크린샷 불필요)

LLM 호출 전 결정론적으로 확정 가능한 항목:

| # | 항목 | 검사 방법 |
|---|---|---|
| A1 | 텍스트 오버플로우 | `Text.preferredWidth > rect.width` 또는 `preferredHeight > rect.height` |
| A2 | 텍스트 truncation | `TMP_Text.isTextOverflowing == true` |
| A3 | 이미지 비율 왜곡 (정성) | `Image.preserveAspect == false` && `rect.aspectRatio != texture.aspectRatio` |
| A4 | 누락 참조 | `Image.sprite == null`, `Text.font == null`, `Image.color.a == 0` 등 |
| A5 | **이미지 비율 왜곡 (정량)** | `\|sprite.rect.aspectRatio - rectTransform.rect.aspectRatio\| ≥ 0.1` → 플래그. 가로/세로 둘 중 하나가 다른 쪽 대비 10% 이상 늘어나면 비율 왜곡 |
| A6 | **LayoutGroup 자식 sanity** | LayoutGroup 부모의 `m_ChildControlHeight`/`m_ChildControlWidth` 확인. <br>- ControlHeight=true: 자식 sizeDelta.y 0이어도 OK (LayoutGroup이 preferredHeight로 결정) <br>- ControlHeight=false: 자식 sizeDelta.y가 0이면 행 사라짐 → 명시적 height 필요 |
| A7 | **stretched anchor + sizeDelta 0 + 부모 미고정** | 자식 anchor(0,0)-(1,1) + sizeDelta(0,0)인데 부모 height/width가 LayoutGroup/ContentSizeFitter로도 결정 안 되면 0으로 collapse |
| A8 | **anchor 전이(GameObject 부모 변경)** | git/MCP로 GameObject를 다른 부모로 옮긴 경우, 새 부모의 width/height가 원래 부모와 다르면 anchor 재설정 필요. **stretched anchor + sizeDelta 0인 자식은 새 부모 크기 그대로 따라가서 비율 깨짐** |
| A9 | UI Layer 미설정 | TMP/Image GameObject의 `m_Layer ≠ 5(UI)` → 렌더 우선순위·이벤트 처리 이상 가능 |

자동 검사로 확정된 항목은 **스크린샷 없이 즉시 플래그**.

### B. 시각 검수 (스크린샷 + 수치 + LLM 판정)

데이터로 판정 불가, LLM 시각 판정 필요. **각 시각 항목은 가능하면 수치 백업 1개 동반**:

| # | 항목 | 확인 내용 | 수치 백업 |
|---|---|---|---|
| B1 | 정렬/간격 | 요소 간 정렬 어긋남, 간격 불균일 | 인접 요소 anchoredPosition 차이 |
| B2 | 크기/비율 | 버튼/패널 크기 이상 | RectTransform.rect.size 비교 |
| B3 | 겹침/비침 | UI 요소 겹침, 팝업 뒤 UI 비침 (딤 누락) | 두 요소 rect 교집합 검사 |
| B4 | 폰트/색상 일관성 | 같은 역할인데 크기/색상 다름 | TMP fontSize, color RGB 비교 |
| B5 | 극단값 대응 | 긴 텍스트, 큰 숫자, 빈 상태에서 레이아웃 붕괴 | preferredWidth vs rect.width |
| B6 | 상태별 시각 | 버튼 normal/pressed/disabled 구분 | Image.color, label.color 비교 |
| B7 | 전환 이상 | 팝업 열기/닫기 시 깜빡임, 잔상 | (시각만) |
| B8 | 해상도 대응 | 16:9 기준 레이아웃 유지 | (시각만) |
| B9 | **아이콘/이미지 가로:세로 비율** | 원본 sprite 비율과 화면상 비율 차이 | sprite vs rect aspectRatio (A5와 동일) |
| B10 | **반복 행 균등성** | VerticalLayoutGroup/HorizontalLayoutGroup의 행이 같은 크기로 균등 stacked 되는지 | 첫·마지막 클론의 rect.size 비교 |

## 5. 실행 흐름

```
1. 대상 파악
   ├── 인자 있음 → 해당 UI만
   └── 인자 없음 → 현재 씬에서 열 수 있는 UI 전체 식별

2. 자동 검사 (A 카테고리)
   ├── u_editor_gameobject로 UI 트리 순회 (필요 부분만 — 너무 큰 트리는 토큰 초과)
   ├── Text/TMP_Text/Image/RectTransform/LayoutGroup 컴포넌트 검사
   └── 위반 항목 → 자동 플래그 목록

3. 시각 검수 (B 카테고리) — "1 검수 → 1 수치 백업" 규칙
   ├── 1차: 에디터 모드에서 UI 확인 → u_screenshot
   ├── 2차: u_play_control(enter) → UI 열기 → 상태 변경(클릭/탭 전환) → u_screenshot → 다음 상태
   ├── 의심 가는 항목은 SceneView zoom 캡처 (u_editor_sceneview)
   ├── 동적 행은 첫 번째 클론 RectTransform 조회로 실제 rect 확인
   └── 각 스크린샷·수치를 체크리스트 B1~B10으로 판정

4. 리포트 작성 + 파일 저장
```

## 6. 도구 사용

| 도구 | 용도 | 주의사항 |
|---|---|---|
| `u_screenshot` | Game View 캡처 | 해상도 작음 — 픽셀 단위 검증 부족, zoom 별도 |
| `u_editor_sceneview` | Scene View 부분 캡처 | UI 영역 zoom in/out 가능 |
| `u_play_control(enter/exit)` | 플레이 모드 제어 | 진입/종료 후 상태 변화 대기 필요 |
| `u_editor_gameobject(action: get)` | 단일 GameObject의 RectTransform/Active 조회 | 우선 사용 — 가벼움 |
| `u_editor_gameobject(action: hierarchy)` | 트리 순회 | 출력 토큰 초과 위험 — 좁은 노드부터 |
| `u_editor_component(action: list/set_property)` | 컴포넌트 값 조회 | **`set_property`는 play mode에서 차단됨** |
| `u_console` | 런타임 에러 확인 | 자동 검사 단계에 포함 |

### 6.1 MCP 도구 사용 주의사항 (사고 방지)

**경험치 — 다음 패턴은 함정**:
- `u_editor_gameobject(action: find, target: "IconImage")`: 동명 오브젝트가 여러 개 있으면 **씬 모드 결과만** 반환할 수 있음. prefab 내 위치를 정확히 잡고 싶으면 `prefabPath`와 함께 **부모 경로 포함**(`Parent/Child`)
- `u_editor_gameobject(action: delete, target: "IconImage")`: **첫 번째 매칭** 삭제 → 동명 GameObject 다수 존재 시 의도 외 객체 삭제 가능. **삭제 전 `find`로 결과 수 확인 필수**
- `u_editor_component(action: set_property)`: nested struct (`m_Padding.left`, `m_RectTransform.anchoredPosition.x` 등) 직접 지정 불가 → YAML 직접 편집 필요
- `u_editor_component(action: add)`: GameObject가 prefab 안에 있으면 `prefabPath` 지정. find와 마찬가지로 동명 시 씬 우선 매칭 가능성 있음
- prefab YAML 직접 편집 시 한글 텍스트는 `\uXXXX` 이스케이프 형태로 저장됨 → Edit 도구의 old_string에 한글 그대로 쓰면 실패 가능, sed 라인 지정으로 우회

## 7. 사고 방지 워크플로우 (수정 작업 시)

> **중요**: 본 스킬은 리포트 전용이므로 수정 작업은 별도. 그러나 후속 수정 사이클에서 본 스킬의 검수 패턴을 따를 때 적용.

### "1 수정 → 1 검증" 강제
배치 수정 금지. 한 번에 하나씩:

```
for each fix:
  1. [수정 전 측정] 대상 요소의 핵심 수치 기록 — rect.size, anchor, sizeDelta, fontSize
  2. [단일 변경 적용] 한 가지 속성만 변경
  3. [수정 후 측정] 같은 수치 재조회 → 의도한 차이만 발생했는지 확인
  4. [시각 캡처] 스크린샷 + (필요 시) zoom 캡처
  5. [컴파일/에러] u_console로 에러 없음 확인
  6. 다음 수정으로
```

### 복원·이식 시 anchor 재검토 규칙
- git에서 떼온 GameObject 또는 다른 부모로 옮긴 GameObject는 **새 부모의 크기에 anchor가 적합한지** 별도 검증
- stretched anchor + sizeDelta 0인 자식은 새 부모 크기에 따라 비율 왜곡 위험 → 즉시 A8 자동 검사 적용

### LayoutGroup 적용 시 필수 명시 항목
LayoutGroup(VerticalLayoutGroup/HorizontalLayoutGroup) 추가하면 다음을 **명시적으로 설정**(기본값 의존 금지):
- `m_ChildControlWidth` / `m_ChildControlHeight` (true/false 의도)
- `m_ChildForceExpandWidth` / `m_ChildForceExpandHeight`
- `m_Padding` (left/right/top/bottom)
- `m_Spacing`
- `m_ChildAlignment`

설정 후 자식 1개의 rect.size가 의도와 일치하는지 즉시 조회.

## 8. 리포트 형식

```markdown
## UI Review 결과 — <YYYY-MM-DD HH:MM>

**대상**: CraftPopup, InventoryPopup, PlayerStatusHUD
**모드**: 플레이 모드 (2차 검수)

### 🚨 자동 플래그 (코드 검사)
1. [A5] CraftScrollCell.IconImage 비율 왜곡
   - sprite aspectRatio 1.0 vs rect aspectRatio 0.6 (60x100)
   - sizeDelta(60, 0) + stretched anchor — 셀 높이에 따라 세로로 늘어남
   - 위치: CraftPopup/...CellTemplate/CraftScrollCell/IconImage

### ⚠️ 시각 검수: 문제 발견
1. [B10] CraftPopup MaterialItemTemplate(Clone) 행 높이 0
   - 시각: 행이 안 보임
   - 수치: 첫 번째 클론 RectTransform.rect.height = 0
   - 원인: VerticalLayoutGroup.ChildControlHeight=false + 템플릿 sizeDelta.y=0 + LayoutElement 미설정
   - 스크린샷: screenshots/<timestamp>/craftpopup_materials.png

### ✅ 통과
- 정렬/간격: 정상
- 폰트/색상 일관성 유지
- 해상도 16:9 정상

### 📋 요약
- 자동 플래그: 1건 (HIGH)
- 시각 검수: 1건 문제 / 9건 통과
```

## 9. 출력 위치

- 리포트: `document/ui-review/<YYYY-MM-DD-HHMM>.md`
- 스크린샷: `document/ui-review/screenshots/<YYYY-MM-DD-HHMM>/<UI명>.png`

## 10. 제약사항

- **수정 금지**: 코드, 프리팹, Inspector 값 일체 수정 안 함
- **리포트 전용**: 발견한 문제 정리해서 보고만
- **3D 씬 대상 아님**: 3D 오브젝트 검수는 별도 영역 (현재 미구현)
- **자동 플래그는 결정론적 케이스만**: A 카테고리만 자동. B는 반드시 시각+수치 판정
- **의도된 디자인 존중**: 명백한 버그가 아닌 한 디자이너 의도로 추정 → 통과 처리
- **시각 검수만으로 통과 금지**: 비율·크기·정렬 의심은 반드시 수치 백업
