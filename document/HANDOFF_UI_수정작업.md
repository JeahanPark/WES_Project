# UI 수정 작업 인계 문서

> 작성: 2026-06-07 / 목적: `/compact` 전 현재 상태 보존, 이후 이어서 작업
> 작업 방식: **단일 세션 직접 수정** (팀 에이전트 ❌ — 아래 "교훈" 참고)

---

## 1. 한 줄 요약

WES 게임 UI 회귀 수정 중. 핵심 회귀는 대부분 수정·커밋됐으나 **짧은 버튼(Invite 등)의 9-slice 프레임 붕괴(RC-1)가 아직 미해결**. working tree에 미커밋 변경분이 쌓여있고, 임시 crop 파일 정리 필요.

---

## 2. 발단

사용자가 게임 UI를 보고 "문제가 너무 많다 / 인게임·버튼·LobbyRoomPopup·CraftPopup 다 이상하다" 보고.
원인: 그 직전 team-lead의 UI 수정 커밋 3건(아래)이 **회귀(regression)를 유발**.

---

## 3. 커밋 이력 (이미 푸시됨)

| 커밋 | 내용 | 비고 |
|---|---|---|
| `6d87dda` | 코어 텐션 연출 통합(A) | 정상 |
| `75c61ec` | UI 프레임 9-slice 정상화 + 로비 톤 통일 | **RC-1/RC-2 회귀 유발** |
| `6367371` | 로비 룸 풀 테마(나무 패널) + 채팅 정리 | 채팅 과다크화 회귀 |
| `2a67507` | 로비/룸/결과 버튼 PPUM 1→2 | RC-1 부분수정(긴 버튼만 살림, 짧은 버튼 여전히 깨짐) |

---

## 4. 근본 원인 (designer wesqa 정밀 감사로 확정)

### RC-1 — btn_frame 9-slice 붕괴 (★미해결)
- `btn_frame_idle/hover/disabled.png` 바깥 마진을 flood-fill로 투명화(커밋 75c61ec)했더니 나무 프레임 본체가 안쪽(x≈88px)으로 밀림.
- 그런데 `.meta`의 `spriteBorder`가 작아서 **9-slice 코너가 투명 영역에 떨어짐** → 코너=빈칸, 나무가 center로 stretch되며 산산조각.
- **높이 < ~0.0778(84px) 버튼만 붕괴.** 타이틀 버튼(220×84)은 정상, 로비 Create/Enter(200×60)·룸 Invite(작음)·코드입력 Enter/Back은 깨짐.
- PPUM 1→2(커밋 2a67507)는 긴 버튼만 살리고 짧은 버튼은 여전히 깨짐.
- **확인됨(2026-06-07 플레이 검증): 룸 Invite 버튼 여전히 프레임 산산조각.**

### RC-2 — slot_frame 9-slice (대체로 수정됨, 확인 필요)
- slot_frame border 90→450 + 사용처 PPUM 22로 조정. 인벤/제작 셀은 개선, 퀵슬롯 확인 필요.

### 기타 (designer/client가 working tree에 수정 반영, 검증 일부만 됨)
- C-1/C-2: CraftPopup·InventoryPopup **본문 BG(DetailPanel) 런타임 비표시** → 코드 수정(CraftDetailPanel/InventoryDetailPanel/InventoryPopup)
- 팝업 동시오픈/겹침 → InventoryPopup 스택 로직
- #8 인게임 월드 과다밝음 → fog로 다크톤 처리(CoreTensionOverlayWorker/DayNightConfig/Ingame.unity)
- 채팅 패널 과다크화·입력창 톤
- 미니맵 흰박스 placeholder(#9), 버튼 텍스트 대비(D-6) — 진행 중이었음(미완 가능)

---

## 5. 미커밋 변경분 (working tree, `git status`)

```
M btn_frame_idle/hover/disabled.png + .meta   (RC-1 시도)
M slot_frame.png + .meta                        (RC-2)
M QuickSlotHUD.prefab
M LobbyPopup.prefab / LobbyRoomPopup.prefab / LoginPopup.prefab / ResultPopup.prefab
M Ingame.unity
M TestManager.cs                                (캡처용 메서드: TestEnterIngameAndHold, TestPopupHoldForCapture)
M CraftHUDTab.cs / QuickSlotCell.cs
M CraftDetailPanel.cs / InventoryDetailPanel.cs / InventoryPopup.cs  (BG누락·겹침)
M CoreTensionOverlayWorker.cs / DayNightConfig.asset / PlayerCharacter.cs  (#8 톤)
M document/auto/diagrams/class/WES-Class-Overview.canvas  (무관, 세션시작부터)
M screenshot.png                                 (스크래치 — 커밋 X)
?? crop_*.png (다수), slot_preview.png           (임시 검증 크롭 — 정리/삭제 대상)
```

⚠️ 아직 **커밋 안 됨.** 검증 통과 후 커밋할 것. `screenshot.png`·`crop_*.png`·`slot_preview.png`·`WES-Class-Overview.canvas`는 커밋 제외.

---

## 6. 다음 할 일 (우선순위)

1. **RC-1 근본 수정 (★최우선)** — btn_frame이 짧은 버튼(60px 이하)에서도 깨지지 않게:
   - 방법: btn_frame `.meta`의 `spriteBorder`를 실제 나무 프레임 안쪽 경계(flood-fill 후 ~88px 기준)에 맞게 키우고, 사용처 버튼들의 `PixelsPerUnitMultiplier`를 버튼 크기에 맞게 재튜닝. (slot_frame을 border 450+PPUM 22로 고친 것과 동일 패턴)
   - 검증: 로비 Create/Enter, 룸 Invite/START GAME, 코드입력 Enter/Back, ResultPopup 버튼 전부 플레이모드 스샷으로 확인.
2. **전 화면 1회 통합 검증** — 타이틀·로비·코드입력·룸·인게임·제작·인벤. (진입: Start→로비, Create(호스트)→룸, START GAME→인게임, BuildingButton→제작, InventoryButton→인벤. 릴레이 호스트됨)
   - 인게임 진입 후 `TestManager`의 `TestEnterIngameAndHold` 사용 가능.
3. RC-2(퀵슬롯)·C-1/C-2(제작/인벤 BG)·#8 톤·채팅 입력창 최종 확인.
4. 임시 파일 정리(`crop_*.png` 등 삭제) 후 **커밋·푸시**. (커밋 메시지 한국어, Co-Authored-By 제외)

---

## 7. 검증 도구 — wesqa "UI 엑스레이"

스샷 눈대중은 어두운 화면에서 미세 깨짐을 놓침. **런타임 UI 트리의 pos/size/exists를 수치로 측정**하는 wesqa로 판별할 것.
- 위치: `tools/wesqa/` — `from wesqa import WesPoco; g = WesPoco(instance=0)`
- size 비정상(프레임 0크기)·겹침·화면이탈·정렬 어긋남을 수치로 잡음. (메모리: project_wesqa_tool)

---

## 8. 교훈 (재발 방지)

- **팀 에이전트 ❌ 단일 세션 ✅**: 이번에 `ingame-ui` 4-에이전트 팀을 자율 모델로 돌렸더니 **상태 동기화 루프(livelock)**에 빠짐 — "이미 완료/재확인/점유 GO" 류 메시지만 폭증, Unity 에디터(단일 자원) 점유 핑퐁. 결국 팀 디렉터리째 사라짐. **Unity 에디터 작업은 순차·단일자원이라 단일 세션이 정답** (TEAM_PROCESS §0-1 그대로).
- **반복 컨텍스트 주의**: 워치독 폴링 루프 + idle 알림 + 동일 필러 응답이 수백 개 쌓여 **출력 반복붕괴("course/call" 무한반복) 글리치** 유발. 긴 대기 루프 금지, 비대해지면 `/compact`.

---

## 9. 현재 상태 (이 문서 작성 시점)

- Unity: 플레이모드 종료됨, 현재 씬 Intro, 콘솔 에러 0.
- 팀: 없음(종료됨).
- 다음 행동: `/compact` 후 위 §6-1(RC-1 수정)부터 단일 세션으로 진행.
