# 외부 자산 백로그 — InputField 스킨 (I-4)

> 등록: designer-b · 2026-06-05 (QA Phase4 I-4/INFO 권고)
> 상태: 보류 (우선순위 중, 이번 UI 리소스 패스 누락 허용 범위)

## 자산
| 항목 | 내용 |
|---|---|
| 자산명 | inputfield_skin (9-slice) |
| 용도 | LoginPopup·LobbyPopup의 InputField (TMP) 배경 스킨 |
| 우선순위 | 중 |
| 현재 상태 | Unity 내장 sprite(00000000) 유지 — 기능 동작, 톤 미적용 |

## 시각 요구
- 양피지·낡은 명판 톤. 버튼/프레임 세트(wes-ui-frame)와 통일.
- 9-slice 테두리(입력 영역 늘어남). 안쪽은 글자 가독 위해 약간 밝은 양피지.
- 팔레트: bg-earth~panel-brown 흙빛, text-bone 글자 대비.

## 대체/생성 경로
- AI(Gemini): wes-ui-frame 프리픽스 + "old parchment/nameplate input field, recessed writing area" 로 1장 생성 → UI/Frame 편입, 9-slice border 설정.
- 적용 대상 노드:
  - LobbyPopup: `EnterCode/InputField (TMP)` 배경 Image
  - (LoginPopup에 InputField 있으면 동일)

## 비고
QA 통과 요약상 차단 아님. 다음 UI 보강 패스 또는 톤 일관성 정리 시 처리.
