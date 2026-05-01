# 지형 경사 이동 — 세션 인수인계

> **작성일**: 2026-04-26
> **상태**: 설계 합의 완료, 구현 대기
> **인수인계 사유**: 사용자가 신규 MCP 도구 개발 중 → 도구 완성 후 새 세션에서 이어서 진행

---

## 1. 사용자 목표

> "지형메쉬를 깔고 메쉬 위를 움직였으면 좋겠어 경사에 맞춰서"

캐릭터(플레이어/몬스터)가 **경사 지형 메쉬 표면을 따라 자연스럽게 이동**하도록 만들고 싶음.

---

## 2. 현재 상태 (확인 완료)

### 이동 시스템
- 플레이어/몬스터 모두 `NavMeshAgent` 사용 (`updateRotation = false`, 회전은 직접 Slerp)
- 플레이어 이동: `Agent.Move(direction × dt × speed)` — `direction.y = 0` ([CharacterBase.cs:289](../WES/Assets/Scripts/WorldBaseObject/CharacterBase.cs#L289))
- 몬스터 이동: `Agent.SetDestination(target)` ([MonsterStateMachine.cs:81-84](../WES/Assets/Scripts/WorldBaseObject/Monster/MonsterStateMachine.cs#L81-L84))
- 마우스 시점: `Plane(Vector3.up, Vector3.zero)` 즉 **Y=0 평면 가정** ([PlayerCharacter.cs:350](../WES/Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs#L350))

### 맵 구성
- [MapGenerator.cs](../WES/Assets/Scripts/Editor/MapGenerator.cs) 가 `GROUND_TILE_SIZE = 4.5f` 평평한 타일을 격자로 배치
- 그 위에 Synty 데코(나무/바위/언덕)를 얹는 방식
- **경사 지형 자체가 없음** → 이게 근본 원인

---

## 3. 결정된 방향

**Synty 슬로프/언덕 메쉬 + NavMesh 베이크 (Max Slope 조정)**

### 선택지 비교 (논의 결과)

| 안 | 평가 |
|---|---|
| A. Unity Terrain | 톤 안 맞음 (기본 Terrain은 사실적, Synty는 로우폴리 단색) → 기각 |
| **B. Synty 슬로프 메쉬 타일** | **채택** — Synty 패키지에 `SM_Env_Ground_Hill_*`, `SM_Env_Slope_*` 등 슬로프 메쉬 포함, MeshCollider 보유, 톤 일관성 유지 |
| C. NavMesh 버리고 Raycast 그라운딩 | 몬스터 AI 다시 짜야 함 → 기각 |

### 핵심 원리
- Synty 슬로프 메쉬를 `Navigation Static` 마크 → NavMesh 베이크
- `NavMeshAgent.Move()`는 NavMesh 표면에 자동 투영 → Y 보정 무료
- Max Slope 값만 적절히 올리면(45° → 60°) 가파른 지형도 이동 가능

---

## 4. 다음 세션 작업 목록

### 4-1. 사전 확인 (사용자에게 물어볼 것)
- **현재 어떤 Synty 팩을 쓰고 있는지** — POLYGON Nature? Survival? Fantasy Kingdom?
  - 팩에 따라 슬로프 메쉬 prefab 이름/구조가 다름
  - `Assets/Synty/` 또는 유사 경로 탐색해서 슬로프 메쉬 후보 목록 작성

### 4-2. MapGenerator 수정
- 평평한 Ground 타일 일부를 경사 메쉬로 교체하는 로직 추가
- 산지(북) 영역에 경사/언덕 메쉬 우선 배치
- 숲(중앙) ~ 산지 전이 영역에 완만한 경사
- 해안가(남)는 평지 유지

### 4-3. NavMesh 재베이크
- Window > AI > Navigation > Bake 설정
  - Max Slope: 45° → **60°** (또는 적절한 값)
  - Step Height, Agent Radius/Height 검증
- 경사 메쉬들을 모두 `Navigation Static`으로 마크
- `MapGenerator`의 BakeNavMesh 메뉴 갱신

### 4-4. 마우스 시점 보정
- [PlayerCharacter.cs:350](../WES/Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs#L350) 의 `Plane(Vector3.up, Vector3.zero)` 폐기
- 카메라에서 마우스 방향으로 **Physics.Raycast** → 지면 LayerMask
- 경사면에서도 정확한 마우스 타겟 좌표 산출

### 4-5. QA 검증
- Synty 슬로프 위를 플레이어가 걷는지
- 몬스터(NavMeshAgent) 가 슬로프 따라 이동하는지
- 마우스 클릭 위치가 경사면에서도 정확한지
- 너무 가파른 곳에서 못 올라가는 자연스러운 동작 확인

---

## 5. 영향받는 파일 (예상)

| 파일 | 변경 내용 |
|---|---|
| [Assets/Scripts/Editor/MapGenerator.cs](../WES/Assets/Scripts/Editor/MapGenerator.cs) | 슬로프 타일 배치 로직 추가 |
| [Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs](../WES/Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs) | `UpdateMouseLook()` Raycast 방식으로 변경 |
| Assets/Scenes/Ingame.unity | 맵 재생성, NavMesh 재베이크 결과 반영 |
| NavMesh 데이터 (asset) | 재베이크 산출물 |

`CharacterBase.cs`, `MonsterStateMachine.cs` 의 이동 코드는 **수정 불필요** — NavMeshAgent가 알아서 슬로프 처리.

---

## 6. 컨텍스트 보존용 메모

- 현재 git: `main` 브랜치, origin보다 23커밋 앞서있음 (push 안 됨)
- M2(인벤토리 & 콘텐츠) 완료, M3(완성도) 시작 직전
- 미커밋 변경: Scene Review 스킬 설계, MCP 플러그인 확장(McpBridgeInputAction.cs 신규), 아이콘 리임포트, UI 스크립트 소폭 수정
- 사용자가 작업 중인 신규 MCP 도구가 이 작업에 필요해서 잠시 보류 중

---

## 7. 새 세션 시작 시 할 일

1. 이 문서 읽기 (`document/2026-04-26-terrain-slope-handoff.md`)
2. 사용자에게 **"새 MCP 도구 준비 완료됐는지, Synty 어느 팩 쓰는지"** 확인
3. Synty 슬로프 메쉬 prefab 탐색 → 후보 목록 작성
4. `MapGenerator` 수정 → 맵 재생성 → NavMesh 베이크 → QA
