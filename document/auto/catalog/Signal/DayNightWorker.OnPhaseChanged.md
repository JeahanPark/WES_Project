---
name: DayNightWorker.OnPhaseChanged
kind: Event
owner: "[[DayNightWorker]]"
signature: "Action<DayPhase, DayPhase>"
direction: Local
authority: Server (driven by NetworkVariable<int> m_Phase)
frequency: 페이즈 전환 시 (Day ↔ Night)
subscribers: []
status: Active
---

# DayNightWorker.OnPhaseChanged

낮/밤 페이즈가 전환될 때 발사되는 정적 이벤트. `(oldPhase, newPhase)` 두 값을 전달.

## 시그니처

```csharp
public static event Action<DayPhase, DayPhase> OnPhaseChanged;
```

## 발사 조건

- 서버에서 `m_Phase` NetworkVariable이 변경됨
- 변경 사실이 `OnPhaseValueChanged` 콜백을 거쳐 모든 클라이언트에 동기화
- 동기화된 시점에 클라이언트에서 OnPhaseChanged 발사

## 관련

- 발사 주체: [[DayNightWorker]]
- 페이로드 타입: `DayPhase` (enum)
- 구독자: (구독 클래스 시드 시 자동 채워짐)
