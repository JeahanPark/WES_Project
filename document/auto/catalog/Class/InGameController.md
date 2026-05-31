---
name: InGameController
category: Controller
parent: "[[GameController]]"
file_path: WES/Assets/Scripts/Controller/InGameController.cs
role: 인게임 씬의 모든 것을 컨트롤하는 최상위 컨트롤러
status: Active
signals: []
---

# InGameController

인게임 씬에 들어왔을 때 활성화되는 컨트롤러. `GameController<T>` 제네릭 베이스를 통한 싱글톤 패턴.

## 책임 영역

- 인게임 씬 진입 시 초기화
- 월드/유저/UI Worker들의 조정
- 씬 전환 시 정리

## 관련

- 부모: [[GameController]]
- 보조: (추후 Worker 카탈로그가 시드되면 wiki link 채워짐)
