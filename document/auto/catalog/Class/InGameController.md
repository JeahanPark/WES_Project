---
name: InGameController
category: Controller
parent: "[[NetworkGameController]]"
file_path: WES/Assets/Scripts/Controller/InGameController.cs
role: 인게임 씬의 모든 것을 컨트롤하는 최상위 컨트롤러
status: Active
signals: []
---

# InGameController

인게임 씬에 들어왔을 때 활성화되는 컨트롤러. `NetworkGameController<InGameController>` 제네릭 베이스를 통한 씬 단위 싱글톤 패턴.

`NetworkGameController<InGameController>` 베이스를 상속해 RPC가 클라이언트에서도 동작한다.

## 책임 영역

- 인게임 씬 진입 시 초기화
- 월드/유저/UI Worker들의 조정
- 씬 전환 시 정리
- 클라 준비 동기화(`NotifyClientReadyServerRpc` → 전원 준비 시 `StartGameClientRpc`), 탈출/게임오버 RPC

## 네트워크

- **자체 `m_NetworkObject` 필드 제거(버그 수정)**: 베이스 `NetworkBehaviour`가 제공하는 `NetworkObject` 프로퍼티와 이름이 겹쳐 NGO 직렬화 규칙을 위반했다. 이제 베이스의 `NetworkObject` / `IsSpawned`를 직접 사용한다(스폰 대기 `WaitUntil(() => IsSpawned)`).

## 관련

- 부모: [[NetworkGameController]] (RPC 가능 씬 컨트롤러 베이스)
- 보조: (추후 Worker 카탈로그가 시드되면 wiki link 채워짐)
