---
name: MoveCostWorker
category: Worker
parent: "[[NetworkBehaviour]]"
file_path: WES/Assets/Scripts/Worker/MoveCostWorker.cs
role: "이동 비용 (이동비용 기획, ④분기). 서버 전용. 이동 = 체온 비용 지불 — 이동 중 Cold(추위)가 가속 누적된다(신규 스탯 없이 기존 Cold 재사용). 이동 감지는 NavMeshAgent가 오너에만 활성이라 서버에서 직접 못 보므로, NetworkTransform이 동기한 위치 델타로 판정(전 플레이어 서버측 가능). 지역 배수(WorldAreaInfo.MoveCostMultiplier, T1) × 날씨 배수(WeatherInfo.MoveCostMul, T1) × 야간 배수로 가중. DayNightWorker와 동일 GameObject에 부착."
status: Active
signals: []
---

# MoveCostWorker

이동 비용 (이동비용 기획, ④분기). 서버 전용. 이동 = 체온 비용 지불 — 이동 중 Cold(추위)가 가속 누적된다(신규 스탯 없이 기존 Cold 재사용). 이동 감지는 NavMeshAgent가 오너에만 활성이라 서버에서 직접 못 보므로, NetworkTransform이 동기한 위치 델타로 판정(전 플레이어 서버측 가능). 지역 배수(WorldAreaInfo.MoveCostMultiplier, T1) × 날씨 배수(WeatherInfo.MoveCostMul, T1) × 야간 배수로 가중. DayNightWorker와 동일 GameObject에 부착.

## 관련

- 부모: [[NetworkBehaviour]]
