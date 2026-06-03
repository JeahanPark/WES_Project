---
name: ColdDamageWorker
category: Worker
parent: "[[NetworkBehaviour]]"
file_path: WES/Assets/Scripts/Worker/ColdDamageWorker.cs
role: 추위(Cold) 위협 워커 — 밤 Cold 누적 + 단계별 HP 틱 데미지 (서버 전용)
status: Active
signals: []
---

# ColdDamageWorker

추위(Cold) 실질화의 핵심 워커. **서버 전용**. `DayNightWorker`와 **동일 GameObject**에 부착되어, 자연 감쇠(`DayNightWorker.ApplyColdDecay`)와 **병렬**로 동작한다.

## 책임 영역

- **Cold 누적** (`UpdateColdAccumulation`): 밤(`DayPhase.Night`)이면 살아있는 플레이어의 Cold를 `ColdAccumPerSecondNight`(기본 10/s)만큼 증가. 단, **켜진 모닥불 보호 범위(`CampfireProtectRange`, 5m) 내면 누적 스킵** — 자연 감쇠는 계속되므로 Cold가 줄어든다.
- **HP 틱 데미지** (`UpdateColdDamageTick`): Cold 단계(`ColdStage`)에 따라 `PlayerCharacter.TakeEnvironmentDamage(_, allowDeath:false)` 호출. WeakDot(-2/3s), StrongDot(-5/2s). HP 1 보호(즉사 없음).
- **단계 판정** (`GetColdStage`): 30/60/90 임계 → None/Warning/WeakDot/StrongDot.
- 켜진 모닥불 근접 판정(`IsNearActiveCampfire`)은 `WorldBuildingObject.ActiveBuildings` + `IsLit` 순회.

## 핵심 결정 (2026-06-03)

- Cold = **추위 누적**(↑=위험, 0=정상). 켜진 모닥불 근처는 누적만 스킵(적극 감소 X).
- 밤 누적은 이 워커 전담 / 평탄 감쇠는 `DayNightWorker`가 전담(밤 멀티 1.0 고정).

## 관련

- 부모: [[NetworkBehaviour]]
- 주요 협력: [[DayNightWorker]] · [[DayNightConfig]] · [[WorldBuildingObject]] · [[PlayerCharacter]] · [[InGameController]]
- 설계: `document/design/game-design/밤_추위_데미지/기획.md` · `document/design/client-spec/campfire-cold/코드명세.md`
