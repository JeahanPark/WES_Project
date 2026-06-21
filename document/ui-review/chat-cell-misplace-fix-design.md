# 채팅 셀 우상단 렌더 버그 — 수정 코드 설계 (적용 대기)

작성: client / 2026-06-08 / **사전 설계만. 적용은 designer 자가검수 결과 후(team-lead 지시).**

## 전제
- designer가 채팅 ScrollView/Content RT 앵커를 일괄 수정 중. 그걸로 셀이 프레임 안에 정상 표시되면 **코드 수정 불요**.
- 여전히 프레임 밖(또는 0폭)이면 아래 BaseScroll 수정 적용.

## 근본 원인 가설 (정적 분석 기반)
`BaseScroll<TData>`는 `m_ViewportSize`를 **Awake에서 단 1회**만 측정(BaseScroll.cs:29 `UpdateViewportSize`). grid 모드 컬럼수도 Awake 1회(`CalculateGridLayout`, line 77).
- LobbyRoomPopup/InventoryPopup/CraftPopup은 전부 `BasePopup` = **PopupManager 동적 Instantiate**.
- 동적 생성 직후 Awake 시점엔 Canvas 레이아웃이 아직 안 돌아 `viewport.rect.height` / `content.rect.width` = 0 가능.
- `SetData()`는 `UpdateContentSize`만 하고 `UpdateViewportSize`를 **재호출 안 함** → m_ViewportSize가 0으로 고착.
- 셀 stretch anchor(0,1)~(1,1)가 0폭 Content에서 우측 끝(anchorMax x=1)에 0폭으로 찍힘 → "우상단 모서리 점" 증상.

## 회귀 영향 범위 (BaseScroll 공통 = 필수 확인)
| 파생 | 사용처 | 모드 | 위험 |
|------|--------|------|------|
| LobbyRoomChatScroll | LobbyRoomPopup(동적) SetData | vertical | 대상 버그 |
| InventoryScroll | InventoryPopup(동적) SetData | grid 추정 | 같은 결함 잠재 → 수정이 오히려 개선 |
| CraftScroll | CraftPopup(동적) SetData | vertical/grid | 동일 |
| ExampleScroll | 예제 | - | 무관 |

→ 셋 다 동적 팝업이라 **이 수정은 깨뜨릴 위험 낮고 잠재 동일버그도 함께 해소**. 단 적용 후 인벤/제작 스크롤 셀 정렬·스크롤 회귀 캡처 확인 필수.

## 수정안 (최소 침습, 기존 Awake 측정 유지 → 회귀 0 지향)

### 변경 1 — SetData/Initialize/RefreshData 진입 시 레이아웃 값 재측정
`UpdateContentSize()` 직전에 viewport/grid 재측정을 넣는다. 신규 private 헬퍼:
```
// (추가) 레이아웃 의존 값 재측정 — 동적 팝업에서 Awake 시점 rect=0 고착 방지
private void EnsureLayoutMetrics()
{
    if (viewport != null)
        UpdateViewportSize();
    if (m_IsGridMode && content != null)
        CalculateGridLayout();
}
```
호출 지점: `SetData`(line 108 UpdateContentSize 앞), `Initialize`(116 앞), `RefreshData`(124 앞)에 `EnsureLayoutMetrics();` 추가.

### 변경 2 — rect 미확정 시 다음 프레임 보정 (1회)
Awake/SetData 시점에도 rect가 0이면(레이아웃 아직 안 돎) 다음 프레임에 1회 재빌드:
```
// SetData/Initialize 끝에서, viewport 높이가 아직 0이면 다음 프레임 보정 예약
private Coroutine m_PendingRelayout;

private void RequestRelayoutIfNeeded()
{
    if (!isActiveAndEnabled) return;
    bool unresolved = (vertical && viewport.rect.height <= 0f)
                   || (horizontal && viewport.rect.width <= 0f)
                   || (content != null && content.rect.width <= 0f);
    if (!unresolved) return;
    if (m_PendingRelayout != null) StopCoroutine(m_PendingRelayout);
    m_PendingRelayout = StartCoroutine(CoRelayoutNextFrame());
}

private IEnumerator CoRelayoutNextFrame()
{
    yield return null;                 // 레이아웃 1프레임 대기
    Canvas.ForceUpdateCanvases();      // rect 확정 강제
    EnsureLayoutMetrics();
    RefreshVisibleCells();
    m_PendingRelayout = null;
}
```
- WES 규칙: 코루틴 메서드 `Co` 접두사 준수(`CoRelayoutNextFrame`).
- `SetData`/`Initialize` 말미에 `RequestRelayoutIfNeeded();` 호출.
- `OnDestroy`에서 코루틴 정리(누수 방지): `if (m_PendingRelayout != null) StopCoroutine(m_PendingRelayout);`

### 대안 (더 가벼움, 변경 2 생략)
변경 1만으로 충분할 수 있다(SetData가 Start 이후 호출되면 그 시점엔 레이아웃 확정). LobbyRoomPopup은 Start에서 SetData(line 63) → 보통 Start 시점엔 1프레임 레이아웃 됨. **우선 변경 1만 적용해 검증, 부족하면 변경 2 추가**가 회귀 최소.

## WES 컨벤션 체크
- 신규 멤버 `m_PendingRelayout` (private, m_ 접두사) ✓
- 코루틴 `Co` 접두사 ✓
- 매개변수 없음(헬퍼들) — 해당 없음
- 클래스 레이아웃: private 메서드 영역에 추가, 코루틴은 기존 패턴 따름 ✓

## 적용 절차 (결과 후)
1. designer 자가검수 캡처에서 셀이 프레임 밖이면 → 변경 1 적용 → MCP refresh → console 에러 0 확인
2. 인게임/룸 재진입(또는 designer 캡처)로 채팅 셀 프레임 안 정렬 확인
3. **회귀**: InventoryPopup/CraftPopup 열어 셀 정렬·스크롤 정상 캡처 확인
4. 부족하면 변경 2 추가, 2~3 재확인

## 미확정
- InventoryScroll/CraftScroll 정확한 모드(grid vs vertical)는 프리팹 ScrollRect 플래그 미확인 — 적용 전 확인(grid면 CalculateGridLayout 재측정 효과 큼).
