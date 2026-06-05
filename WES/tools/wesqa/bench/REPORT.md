# wesqa 효과 측정 — 심은 버그 검출 (before/after)
> 방식: 실 게임 UI 트리 스냅샷(`snapshot.json`)을 변조한 seed를 가짜 서버로 서빙
> (전송 프레이밍·JSON-RPC는 실 C# 서버와 바이트 동일). 동일 wesqa 클라이언트로 검출 측정.

- 검증 단언 수: 4
- 헬시 baseline: 4/4 GREEN (오프라인 하니스 2ms / **라이브 에디터 실연동 47ms** — 별도 측정)
- 라이브 검증: 실 Unity 플레이모드 LoginPopup 트리에서 스위트 4/4 GREEN 확인 완료

## After — wesqa 자동 스위트

| seed (심은 버그) | 검출 | RED 단언 | 검출시간 |
|---|---|---|---|
| StartButton 삭제 | ✅ CAUGHT | StartButton 존재, StartButton 클릭가능(Button) | 2ms |
| ExitButton 삭제 | ✅ CAUGHT | ExitButton 존재 | 1ms |
| StartButton 이름변경 | ✅ CAUGHT | StartButton 존재, StartButton 클릭가능(Button) | 4ms |
| StartButton 타입손상 | ✅ CAUGHT | StartButton 클릭가능(Button) | 1ms |

**검출율 4/4 (100%) · MTTD 2ms · 결정적(재실행 동일)**

## Before — 수동/에이전트 눈검사 (현 방식)

| 항목 | 값 |
|---|---|
| 검출 보장 | ❌ 없음 — 사람이 놓칠 수 있음 |
| 판정 | 비결정적 — 스크린샷 주관 비교 |
| 1개 상태 소요 | 캡처+육안 수초~수십초 |
| 회귀 N종 | 매 상태 수동 반복 |

## 결론

자동 스위트는 심은 버그 4/4을(를) 평균 2ms에 결정적으로 검출. 수동 방식은 검출 보장이 없고 상태마다 반복 비용이 든다. M2(입력)·M4(Invoke 주입) 도입 시 플로우 구동 후 상태 회귀까지 동일 방식으로 자동 검출 확대 가능.
