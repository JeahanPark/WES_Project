---
name: WeatherWorker
category: Worker
parent: "[[NetworkBehaviour]]"
file_path: WES/Assets/Scripts/Worker/WeatherWorker.cs
role: "날씨 시스템 (날씨_시스템 기획). 서버 권위 NetworkVariable로 전원 동기(§9 협동 동기화). 낮밤 페이즈 전환마다 1틱(§5.3 시간대 동기) — 지역 분포(WorldAreaWeatherInfo, T1)에서 목표를 샘플링하고 심각도 사다리에서 현재 날씨를 목표 방향으로 '한 단계만' 이동(§5.3 급격한 점프 금지 = 전조 보장). 빗나감(후회)의 원천 = 목표 샘플링의 확률성. 시각 전조/오버레이는 designer·sound 영역(백로그)."
status: Active
signals: []
---

# WeatherWorker

날씨 시스템 (날씨_시스템 기획). 서버 권위 NetworkVariable로 전원 동기(§9 협동 동기화). 낮밤 페이즈 전환마다 1틱(§5.3 시간대 동기) — 지역 분포(WorldAreaWeatherInfo, T1)에서 목표를 샘플링하고 심각도 사다리에서 현재 날씨를 목표 방향으로 '한 단계만' 이동(§5.3 급격한 점프 금지 = 전조 보장). 빗나감(후회)의 원천 = 목표 샘플링의 확률성. 시각 전조/오버레이는 designer·sound 영역(백로그).

## 관련

- 부모: [[NetworkBehaviour]]
