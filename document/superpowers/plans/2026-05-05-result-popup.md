---
작성일: 2026-05-05
스펙: ../specs/2026-05-05-result-popup-design.md
---

# 구현 계획 — 결과 팝업

## Task 1: ResultPopup.cs
- BasePopup 상속, `[SerializeField]` 필드 3개 (타이틀/부제 TMP_Text, 확인 Button)
- `ShowResult(GameState)` 메서드 — 텍스트/색상 설정
- `OnClickConfirm` — 버튼 비활성 후 `Managers.Scene.LoadScene(SCENE_LOBBY)`

## Task 2: ResultPopup.prefab
- `generate_ui_with_gpt` MCP 도구로 자동 생성 시도
- 실패 시 InventoryPopup 구조 참조해서 수동 또는 코드 생성
- 결과: 중앙 패널 + 타이틀 텍스트 + 부제 텍스트 + 확인 버튼

## Task 3: 프리팹 ResultPopup 컴포넌트 부착 + 필드 연결
- `u_editor_component add` 또는 prefab 직접 편집
- TMP_Text/Button 참조를 set_reference로 연결

## Task 4: Addressable 등록
- `u_editor_asset` 또는 직접 group에 추가
- Address: `ResultPopup`

## Task 5: InGameController 수정
- `TriggerClearClientRpc`/`TriggerGameOverClientRpc`에서:
  - `StartCoroutine(CoReturnToLobby(3f))` 제거
  - `Managers.Popup.Open<ResultPopup>()?.ShowResult(m_GameState)` 호출

## Task 6: TestManager QA 시나리오
- `TestResultPopupOnGameOver`:
  - 플레이어 죽임 → 결과 팝업 열림 확인 + 텍스트 확인

## Task 7: 컴파일 + QA + 커밋
