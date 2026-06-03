---
name: DayNightConfig
category: Domain
parent: "[[ScriptableObject]]"
file_path: WES/Assets/Scripts/Config/DayNightConfig.cs
role: 낮밤·추위 시스템 수치 ScriptableObject (페이즈 시간·Cold 감쇠/누적/단계·시야·라이트)
status: Active
signals: []
---

# DayNightConfig

낮밤·추위 시스템의 모든 튜닝 수치를 담는 ScriptableObject. 에셋 인스턴스: `Assets/GameResource/Config/DayNightConfig.asset`.

## 책임 영역

- **페이즈 시간**: Day/Dusk/Night/Dawn 지속(초). `GetPhaseDuration`.
- **Cold 감쇠 멀티**: 페이즈별 자연 감쇠 배율. `GetColdMultiplier`. **밤=1.0 고정(결정2, 2026-06-03)** — 옛 2.0(체온모델 잔존)을 제거.
- **Cold 누적/단계**: `ColdAccumPerSecondNight`(10), 단계 임계 30/60/90, WeakDot(-2/3s)·StrongDot(-5/2s), `CampfireProtectRange`(5m).
- **시야·라이트·오버레이**: 페이즈별 ambient/intensity/night overlay alpha.

## 관련

- 부모: [[ScriptableObject]]
- 소비자: [[DayNightWorker]] (감쇠·시간) · [[ColdDamageWorker]] (누적·틱·단계) · [[DayNightRenderWorker]] (라이트)
