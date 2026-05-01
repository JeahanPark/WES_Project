---
paths:
  - "Assets/Scripts/WorldBaseObject/**"
---

# WorldBaseObject 계층 구조

월드에 존재하는 오브젝트 상속 계층.

```
WorldBaseObject (최상위)
├── WorldEntityBase (엔티티 베이스)
│   ├── CharacterBase
│   │   ├── Player/PlayerCharacter
│   │   └── Monster/Test01Monster (MonsterStateMachine 사용)
│   └── (확장 가능)
├── WorldBuildingObject (건물)
└── WorldDropItem (드롭 아이템)
```

- `CharacterScriptable.cs` — 캐릭터 ScriptableObject 데이터
- `EscapePoint.cs` — 탈출 포인트
- `GameAnimationComponent.cs` — 공통 애니메이션
- `Player/PlayerAnimationComponent.cs` — 플레이어 애니메이션
- `Monster/State/` — 몬스터 FSM (Idle, Walk, Hit, Death)
