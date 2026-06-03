---
name: DayNightWorker
category: Worker
parent: "[[NetworkBehaviour]]"
file_path: WES/Assets/Scripts/Worker/DayNightWorker.cs
role: 낮밤 페이즈 사이클(서버) + 페이즈별 Cold 자연 감쇠
status: Active
signals: ["[[DayNightWorker.OnPhaseChanged]]"]
---

# DayNightWorker

낮밤 페이즈(Day→Dusk→Night→Dawn) 사이클을 서버에서 진행하고, 페이즈 변경을 `OnPhaseChanged`로 방송한다. 추위 시스템의 **자연 감쇠**도 담당.

## 책임 영역

- **페이즈 사이클**: `m_Config.GetPhaseDuration`만큼 경과 시 `AdvancePhase`. `m_Phase`(NetworkVariable) 동기화 → `OnPhaseChanged(old,new)` 발사.
- **Cold 자연 감쇠** (`ApplyColdDecay`): `BaseColdDecayPerSecond × GetColdRateMultiplier`만큼 매 틱 Cold 감소(회복). **밤 멀티 1.0 고정**(결정2, 2026-06-03 — 옛 2.0 제거).
- 밤 누적·HP 틱은 [[ColdDamageWorker]]가 별도 전담(동일 GameObject, 병렬).
- `#if UNITY_EDITOR ForcePhase` — QA/테스트용 페이즈 강제.

## 관련

- 부모: [[NetworkBehaviour]]
- 주요 협력: [[DayNightConfig]] · [[ColdDamageWorker]] · [[DayNightRenderWorker]]
- 발사 시그널: [[DayNightWorker.OnPhaseChanged]]
