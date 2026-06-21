# 인게임 흐릿(회청색 안개) 원인 분석 — client 정적 분석

작성: client (ui-audit) / 2026-06-08 / 정적 분석만(플레이모드 미점유)

## 결론 (한 줄)
`CoreTensionOverlay`(풀스크린 연출 그룹)가 **HUD 캔버스의 마지막 자식 = 모든 UI 위에 그려지는데**, 그 안의 **AmbientFog가 알파 1.0으로 상시 활성** + **DayNightTint Day알파 0.4 회청색**이라 월드·UI가 함께 뿌예진다.

## 근거 (파일:라인)

### 원인 1 — AmbientFog 상시 불투명 (주범)
- `Assets/Scenes/Ingame.unity:87452` → AmbientFog(RawImage) `m_Color a: 1`
- `Ingame.unity:87434-87437` → anchor 0~0 / 1~1, sizeDelta 0 = 풀스크린
- `CoreTensionOverlayWorker.cs:66` 슬롯이지만, **알파를 동적으로 제어하는 로직 없음** — fog는 알파 1.0 고정으로 항상 화면 전체에 깔린다(UpdateFogScroll은 uvRect만 스크롤, 알파 미조정).
- 텍스처 `ambient_fog.png` `sRGBTexture:1`, 풀스크린 타일 → 안개 텍스처 자체가 화면을 덮음.

### 원인 2 — DayNightTint Day 알파/색 과다 (회청색 색조 주범)
- `Ingame.unity:43824` `m_TintDay: {r:0.24, g:0.3, b:0.4}` = 어두운 청색
- `Ingame.unity:43828` `m_TintAlphaDay: 0.4` (스크립트 기본값 0.18의 2배 이상)
- 결과: Day 페이즈 상시 어두운 청색 틴트 0.4 → "회청색 안개" 색조와 정확히 일치
- (참고) `CoreTensionOverlayWorker.cs:54,59` 코드 기본값은 `(0.49,0.54,0.59)`/0.18 이지만 **씬 인스턴스 값이 오버라이드**되어 더 진함.

### 원인 3 — 오버레이가 HUD 위에 그려짐 (UI까지 흐려지는 이유)
- `Ingame.unity:44891-44897` InGameHUDWorker 자식 순서: PlayerStatus, Craft, QuickSlot, Phase, Toast, **CoreTensionOverlay(961387418)가 마지막**
- UGUI는 자식 순서가 곧 렌더 순서 → CoreTensionOverlay가 모든 HUD 위. fog/tint가 HUD를 덮어 HUD도 뿌예짐.

## 해결안 (택1 또는 조합, 코드 수정은 승인 후 별도)

| # | 조치 | 위치 | 효과 | 비용 |
|---|------|------|------|------|
| A | AmbientFog 알파 1.0 → 0.1~0.2로 낮춤 | 씬 `Ingame.unity:87452` (인스펙터 m_Color a) | 안개 농도 즉시 완화, 코드 무수정 | S |
| B | m_TintAlphaDay 0.4 → 0.15 복원, m_TintDay 밝게 | 씬 `Ingame.unity:43824,43828` | 회청색 색조 제거 | S |
| C | CoreTensionOverlay를 HUD 자식에서 **HUD보다 아래 형제(또는 별 캔버스 sortingOrder 낮게)**로 이동 | 씬 계층 | 월드만 어둡게, UI는 선명 유지 | S~M |
| D | fog/tint를 UI 캔버스가 아닌 **월드 카메라 post(URP Volume)**로 이관 | 렌더 파이프라인 | 근본 해결(UI 비오염), 그러나 재작업 | L |

권고: **A+B로 즉시 농도/색조 완화** → 그래도 UI가 흐리면 **C로 오버레이를 HUD 아래로**. D는 백로그.

## 미해결 / designer 확인 요청
- ambient_fog가 의도상 "월드만" 덮어야 하는지 "화면 전체 연출"인지 = 디렉터/디자이너 결정 영역.
- 런타임에서 AmbientFog만 비활성 시 UI 선명해지는지 designer 플레이 확인 권장.

## 주의 (값 출처)
- 흐림 강도는 **코드 상수가 아니라 씬 인스턴스 값**(TintAlphaDay 0.4, AmbientFog alpha 1.0)이 지배. 코드만 봐선 0.18로 보이니 반드시 씬 값 기준으로 조정할 것.

## 2차 검증 (designer 캡처 + 전 레이어 알파 추적, 2026-06-08)
designer 관찰: CoreTensionOverlay 아래 7~8장 풀스크린 Image가 visible=True. 하지만 **visible≠흐림기여.** Day 페이즈(게임 시작) 기준 각 레이어 실제 알파:

| 레이어 | Day 시작 알파 | 출처 | 흐림 기여 |
|--------|------|------|----------|
| AmbientFog | **1.0** | Ingame.unity:87452 (코드 미제어) | **주범** |
| DayNightTint | **0.4** 청색{0.24,0.3,0.4} | Ingame.unity:43824/43828 | **2차** |
| ColdOverlay1/2/3 | 0 | 씬 초기 a:0 (예: 9896), 추위 None→CoFadeColdStage 0 | 없음 |
| HpVignette | 0 | OnHPChanged 정상HP→a:0 | 없음 |
| DeathOverlay | 0 | PlayDeathFade 전 a:0 | 없음 |
| DarknessOverlay (NightVisionRoot) | **0** | Ingame.unity:16335 a:0 + GetNightOverlayAlpha(Day)=0 (DayNightConfig.cs:110) | 없음(Day) |

**결론: 7~8장 중 실제 흐림 기여는 AmbientFog(1.0)+DayNightTint(0.4) 2장뿐.** 나머지 5장은 알파 0으로 무해.
→ designer는 **AmbientFog, DayNightTint 2개만** 토글 검증하면 충분(나머지는 이미 투명).
→ 해결안 A+B로 충분히 해소될 것(C는 그래도 UI 흐릴 때만). 코드 토글 훅 제작 불요.

---

# UI 비침범 보장 방식 (사용자 결정 반영, 2026-06-08)

사용자 결정: **코어텐션 연출(AmbientFog/DayNightTint/CoreTensionOverlay/DarknessOverlay)은 UI/HUD에 절대 영향 금지. 효과는 월드에만, UI는 항상 선명.** → C(구조 분리)는 필수. A+B(강도 조정)는 월드 연출 품질용으로 병행.

## 현재 캔버스 구조 (Ingame.unity)
단일 Canvas (`Screen Space - Camera`, UICamera=URP Overlay, sortingOrder 0). 자식 형제 순서(=렌더 z순, 아래→위):
```
Canvas (1524037883)
├ [1] InGameHUDWorker (979404607)          ← 맨 먼저 그려짐(맨 아래)
│   └ PlayerStatus/Craft/QuickSlot/Phase/Toast
│   └ CoreTensionOverlay (961387418)        ← HUD GameObject "안" 맨끝 → HUD 형제들을 덮음
│       └ DayNightTint, AmbientFog, Cold1~3, HpVignette, DeathOverlay
├ [2] InGameWorldUIWorker (752214838)
├ [3] NightVisionRoot (931324980)           ← HUD보다 위 → HUD 덮음
│   └ DarknessOverlay
└ [4] DeathOverlay (1779759608)             ← 캔버스 최상단(별도, 사망 암전 의도)
```
구조 결함 2가지:
- (a) CoreTensionOverlay가 InGameHUDWorker **자식**이라 같은 HUD 캔버스 안에서 HUD 위에 그려짐.
- (b) NightVisionRoot가 HUD보다 형제 순서 위 → 밤 암전이 HUD를 덮음.

→ 모두 **같은 단일 캔버스**라 sortingOrder만으로는 HUD와 연출을 분리할 수 없다(같은 캔버스 내부는 형제 순서로만 정렬, HUD를 항상 위로 강제 불가).

## 권장안: 연출 전용 캔버스를 HUD 캔버스보다 "뒤(아래)"로 분리 (방식 C-1)

WES 구조 적합성: 추가 카메라/레이어 도입 없이, 기존 단일 UICamera를 유지하면서 **캔버스 2개로 분리 + sortingOrder로 HUD를 항상 위**로 보장. 비용 S~M, 코드 거의 무수정(슬롯 참조 유지).

### 신규 구조
```
UICamera (그대로)
├ Canvas_CoreTension (신규, sortingOrder = -10)   ← 연출 전용, 항상 HUD 아래
│   ├ DayNightTint, AmbientFog, Cold1~3, HpVignette
│   ├ NightVisionRoot/DarknessOverlay (여기로 이동)
│   (CoreTensionOverlayWorker, NightVisionComponent 부착)
├ Canvas_HUD (= 기존 Canvas, sortingOrder = 0)     ← HUD 전용, 항상 위 → 선명
│   ├ InGameHUDWorker (PlayerStatus/Craft/QuickSlot/Phase/Toast)
│   └ InGameWorldUIWorker
└ Canvas_DeathFade (신규 or 기존 [4] 승격, sortingOrder = +10)  ← 사망 암전만 예외적으로 최상단
    └ DeathOverlay
```
- **연출(틴트/안개/추위/밤)은 sortingOrder -10 캔버스** → 무조건 HUD 아래. HUD는 항상 선명(요구 충족).
- **DeathOverlay만 예외**: 사망 암전은 "화면 전체 암전 후 결과"가 의도된 연출(PlayDeathFade, CoreTensionOverlayWorker.cs:250-263)이라 HUD 위 최상단 유지. → director/사용자 확인 필요(아래 미해결).

### 적용 단계 — 확정 (사용자 결정 2026-06-08 반영, designer/qa 점검 종료 후 일괄)
**스크립트 코드 수정 없음.** 전부 씬(Ingame.unity) 계층/컴포넌트 + 인스펙터 값 작업. 슬롯 참조는 GameObject 이동해도 fileID 유지(검증 필요).

최종 분리: Canvas_CoreTension(-10) = DayNightTint·AmbientFog·Cold1~3·**HpVignette**·NightVisionRoot / Canvas_HUD(0) = HUD·WorldUI / DeathFade(+10) = 사망 암전만.

> **핵심 주의 — DeathOverlay 슬롯 정체 확정:**
> CoreTensionOverlayWorker.m_DeathOverlay = **fileID 1803167125** (= CoreTensionOverlay **자식**, Ingame.unity:43821/81921). 이게 실제 작동하는 사망 암전.
> 캔버스 최상단의 별도 DeathOverlay(1779759608, Ingame.unity:70534/81420)는 **어느 Worker 슬롯에도 연결 안 됨 = 고아/구버전 잔재.**
> → 사망 암전을 HUD 위(+10)로 유지하려면 **1803167125를 CoreTensionOverlay 그룹에서 빼서 +10 캔버스로 옮겨야** 한다(안 빼면 -10 그룹에 딸려가 HUD 아래로 내려감 = Q1 결정 위반). 고아 1779759608은 정리(삭제) 권장.

| 단계 | 작업 | 대상 fileID / 파일:라인 | 값 |
|------|------|------|----|
| 1 | 신규 `Canvas_CoreTension` 생성: Canvas(renderMode=ScreenSpaceCamera, camera=UICamera **115749251**, **sortingOrder -10**) + CanvasScaler(1920x1080, 기존과 동일) + GraphicRaycaster는 **제거**(연출은 raycast 불필요, HUD 클릭 막지 않게) | 씬 신규 | sortingOrder=-10 |
| 2 | `CoreTensionOverlay`(961387418)를 InGameHUDWorker 자식에서 → **Canvas_CoreTension 자식으로 이동** | Ingame.unity:44897(자식목록 제거) → 신규 캔버스 | — |
| 3 | `NightVisionRoot`(931324980)를 루트 Canvas 자식에서 → **Canvas_CoreTension 자식으로 이동** | Ingame.unity:70533 | — |
| 4 | **HpVignette(485188502)는 별도 이동 불요** — 이미 CoreTensionOverlay 자식이라 2단계로 함께 -10 캔버스로 따라감. (Worker 슬롯 m_HpVignette=485188502 fileID 유지) | Ingame.unity:43820 | 확인만 |
| 5 | **DeathOverlay 분리(Q1 예외)**: 실제 연결된 **1803167125**(CoreTensionOverlay 자식)를 → 신규 `Canvas_DeathFade`(sortingOrder **+10**, 동일 UICamera/Scaler) 자식으로 이동. CoreTensionOverlayWorker.m_DeathOverlay 슬롯(43821)은 fileID 유지돼 재연결 불요 | 1803167125 → 신규 +10 캔버스 | sortingOrder=+10 |
| 6 | **고아 DeathOverlay 정리**: 1779759608(미연결)은 삭제 또는 비활성. (삭제 전 다른 참조 0 재확인) | Ingame.unity:70534/81420 | 삭제 권장 |
| 7 | 슬롯 fileID 유지 검증: InGameHUDWorker.m_CoreTensionOverlay(44922), m_HpVignette/m_DeathOverlay(43820/43821) 모두 이동 후에도 연결 살아있는지 MCP refresh 후 인스펙터 확인 | 검증 | — |
| 8 | (병행, 월드 농도) A: AmbientFog 알파 1.0→0.15 (Ingame.unity:87452 m_Color a), B: m_TintAlphaDay 0.4→0.15 (43828) + m_TintDay 밝게(예: {0.45,0.5,0.55}, 43824) | 값 조정 | A·B |

**적용 주체**: 5·6단계 포함 전부 씬 계층 이동/삭제라 **designer 영역**(Unity 점유). client는 슬롯 fileID 유지 검증(7단계)·값(8단계) 자문. designer 점유 종료 후 누가 적용할지 그때 조율.

### 대안 (참고, 비채택 사유)
- C-2 (형제 순서만 재배치, 단일 캔버스 유지): CoreTensionOverlay/NightVisionRoot를 HUD보다 **앞 형제**로 이동. 비용 최소(S)지만, 단일 캔버스라 향후 팝업/토스트가 또 끼면 재발 가능 + DeathOverlay 예외 처리 까다로움. **구조적 보장이 약해 비권장.**
- C-3 (연출을 메인(월드) 카메라 post-process로 이관, 해결안 D): URP Volume(Vignette/ColorAdjustments)로 이관 → 물리적으로 UI와 분리(가장 견고). 단 AmbientFog 스크롤·추위 단계별 텍스처·밤 라이트 컷아웃 셰이더(NightVisionComponent의 WorldToScreen 원형 광원)는 Volume로 재현 어려움 → **대규모 재작업(L), 백로그.**

→ **C-1(연출 전용 캔버스 sortingOrder 분리) 권장**: 요구(UI 비침범) 구조적 보장 + 기존 Worker/슬롯 코드 보존 + 비용 S~M.

## 결정 완료 (사용자 2026-06-08)
- **Q1. 사망 암전(DeathOverlay): HUD 위 유지(예외).** sortingOrder +10 별도 캔버스. 사망=게임오버라 화면 전체 덮는 게 의도. → 5단계로 실제 연결된 1803167125를 +10 캔버스로 분리.
- **Q2. HpVignette(저체력 비네팅): UI 아래(엄격).** Canvas_CoreTension(-10)에 포함. HUD 모서리 살짝 가려도 비침범 원칙 우선. → 이미 CoreTensionOverlay 자식이라 2단계로 자동 -10 이동(4단계 확인만).
- → 적용 단계표 8단계 확정. 실제 씬 적용은 designer Unity 점유 종료 후.
