---
name: CoreTensionOverlayWorker
category: Worker
parent: "[[MonoBehaviour]]"
file_path: WES/Assets/Scripts/Worker/CoreTensionOverlayWorker.cs
role: "코어텐션 풀스크린 오버레이 구동 Worker (클라이언트 로컬 연출 전용). 리소스리스트 G-2(추위)/G-3(낮밤)/G-13(저체력 비네팅)/G-14(사망)/I-5(앰비언트 안개) 와이어링.  모든 오버레이는 로컬 플레이어의 이미 동기화된 상태(Cold/HP NetworkVariable)와 DayNightWorker.OnPhaseChanged(서버 권한, 전원 동기화) 이벤트를 클라이언트가 읽어 화면 연출만 한다. 신규 NetworkVariable/Rpc 없음.  텍스처 소스: Assets/GameResource/UI/CoreTension/ (CoreTensionTextureSetup이 생성). 실제 sprite/GameObject 슬롯 연결은 designer-b가 프리팹에서 수행."
status: Active
signals: []
---

# CoreTensionOverlayWorker

코어텐션 풀스크린 오버레이 구동 Worker (클라이언트 로컬 연출 전용). 리소스리스트 G-2(추위)/G-3(낮밤)/G-13(저체력 비네팅)/G-14(사망)/I-5(앰비언트 안개) 와이어링.  모든 오버레이는 로컬 플레이어의 이미 동기화된 상태(Cold/HP NetworkVariable)와 DayNightWorker.OnPhaseChanged(서버 권한, 전원 동기화) 이벤트를 클라이언트가 읽어 화면 연출만 한다. 신규 NetworkVariable/Rpc 없음.  텍스처 소스: Assets/GameResource/UI/CoreTension/ (CoreTensionTextureSetup이 생성). 실제 sprite/GameObject 슬롯 연결은 designer-b가 프리팹에서 수행.

## 관련

- 부모: [[MonoBehaviour]]
