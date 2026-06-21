---
name: InGameAreaBandWorker
category: Worker
parent: "[[NetworkBehaviour]]"
file_path: WES/Assets/Scripts/Worker/InGameAreaBandWorker.cs
role: "R2 6지역 공간 (출시_6지역_공간 코드명세). 서버 권위. 선두 플레이어(살아있는 플레이어 중 종단축 진행 깊이 최대)의 좌표 → WorldAreaInfo.AxisMin/AxisMax(CSV 단일 진실원)로 현재 지역 d 판정 → 변경 시 WeatherWorker.SetArea(d)·MoveCostWorker.SetArea(d) 호출 → 전원이 그 지역 환경 공유. [확정] 전역 1분포(개별 아님). m_CurrentAreaId 단조증가(전진만) — 선두 사망 승계로 선두가 얕아져도 후퇴 안 함. [확정] 경계 깜빡임 방지 = 종단축 데드존 히스테리시스(경계를 m_BandHysteresis 이상 넘어야 전환). DayNightWorker GameObject(WeatherWorker/MoveCostWorker와 동일 오브젝트, SetArea 타깃)에 부착."
status: Active
signals: []
---

# InGameAreaBandWorker

R2 6지역 공간 (출시_6지역_공간 코드명세). 서버 권위. 선두 플레이어(살아있는 플레이어 중 종단축 진행 깊이 최대)의 좌표 → WorldAreaInfo.AxisMin/AxisMax(CSV 단일 진실원)로 현재 지역 d 판정 → 변경 시 WeatherWorker.SetArea(d)·MoveCostWorker.SetArea(d) 호출 → 전원이 그 지역 환경 공유. [확정] 전역 1분포(개별 아님). m_CurrentAreaId 단조증가(전진만) — 선두 사망 승계로 선두가 얕아져도 후퇴 안 함. [확정] 경계 깜빡임 방지 = 종단축 데드존 히스테리시스(경계를 m_BandHysteresis 이상 넘어야 전환). DayNightWorker GameObject(WeatherWorker/MoveCostWorker와 동일 오브젝트, SetArea 타깃)에 부착.

## 관련

- 부모: [[NetworkBehaviour]]
