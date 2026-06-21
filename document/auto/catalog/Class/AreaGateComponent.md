---
name: AreaGateComponent
category: Component
parent: "[[NetworkBehaviour]]"
file_path: WES/Assets/Scripts/Component/AreaGateComponent.cs
role: "R2 6지역 공간 관문 펌프(일방향) — 출시_6지역_공간 코드명세 §3.2. 서버 권위. [확정] 펌프 = 좌표게이트. 통과 후 역행 차단. 전원통과까지 관문 유지(뒤처진 인원 통과 허용 후 봉쇄). 슬라이스1: 판정 로직(통과 추적 + 역행 차단 판정 + 봉쇄벽 토글)까지. 실제 씬 배치/지오메트리는 슬라이스2(level-design). 봉쇄는 보이지 않는 BoxCollider 벽(m_BlockWall) on/off — 서버 위치 푸시백보다 클라 이동예측 떨림이 적음(§11 권고)."
status: Active
signals: []
---

# AreaGateComponent

R2 6지역 공간 관문 펌프(일방향) — 출시_6지역_공간 코드명세 §3.2. 서버 권위. [확정] 펌프 = 좌표게이트. 통과 후 역행 차단. 전원통과까지 관문 유지(뒤처진 인원 통과 허용 후 봉쇄). 슬라이스1: 판정 로직(통과 추적 + 역행 차단 판정 + 봉쇄벽 토글)까지. 실제 씬 배치/지오메트리는 슬라이스2(level-design). 봉쇄는 보이지 않는 BoxCollider 벽(m_BlockWall) on/off — 서버 위치 푸시백보다 클라 이동예측 떨림이 적음(§11 권고).

## 관련

- 부모: [[NetworkBehaviour]]
