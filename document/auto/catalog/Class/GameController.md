---
name: GameController
category: Controller
parent: null
file_path: WES/Assets/Scripts/Controller/GameController.cs
role: 모든 씬 컨트롤러의 추상 베이스 (제네릭 싱글톤)
status: Active
signals: []
---

# GameController

추상 제네릭 베이스 클래스. `GameController<T> : MonoBehaviour where T : GameController<T>` 형태로 자기 자신을 타입 인자로 받아 정적 인스턴스를 제공한다.

## 자식 컨트롤러

- [[InGameController]]
- (다른 컨트롤러는 시드 시 자동 채워짐)
