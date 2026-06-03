---
name: WorldBuildingObject
category: Domain
parent: "[[WorldBaseObject]]"
file_path: WES/Assets/Scripts/WorldBaseObject/WorldBuildingObject.cs
role: 월드 건물(모닥불·횃불) — 점화 상태(m_IsLit)와 켜진 건물 정적 목록 관리
status: Active
signals: []
---

# WorldBuildingObject

월드에 설치되는 건물(모닥불 Id=1, 횃불 Id=2). NetworkBehaviour 계열.

## 책임 영역

- **점화 상태** `m_IsLit` (NetworkVariable<bool>, Server Write/Everyone Read, 초기값 true): `SetLit`/`IsLit`. 꺼지면(`!IsLit`) 효과 Update 스킵.
- **정적 목록** `ActiveBuildings`: OnNetworkSpawn/Despawn에서 등록·해제. `ColdDamageWorker.IsNearActiveCampfire`가 켜진 모닥불 근접 판정에 순회.
- 건물 식별 `m_BuildingInfoId` (`SetBuildingInfoId`/`BuildingInfoId`).

## 변경 이력

- 2026-06-03: 옛 체온모델 잔존 `ApplyCampfireEffect`의 `SetCold(Cold + 2)`(불 옆 추위 증가, 역방향) **제거**. 추위 모델에서 모닥불 보호는 [[ColdDamageWorker]]가 "누적 스킵 + 자연 감쇠"로 처리(결정1).

## 관련

- 부모: [[WorldBaseObject]]
- 주요 협력: [[ColdDamageWorker]] (켜진 불 보호 판정) · BuildingInfo CSV(Id→PrefabKey)
