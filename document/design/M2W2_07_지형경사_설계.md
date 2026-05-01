# 지형 경사 이동 — 설계서

> **작성일**: 2026-04-26
> **상태**: 설계 합의 완료, 구현 계획 대기
> **선행 문서**: [2026-04-26-terrain-slope-handoff.md](./2026-04-26-terrain-slope-handoff.md)

---

## 1. 목표

캐릭터(플레이어/몬스터)가 Synty 슬로프/언덕 메쉬 표면을 따라 자연스럽게 이동한다.
구현 방식은 **NavMeshAgent + Synty Slope 메쉬 + 디자인 손맛(하이브리드)** 으로,
코드가 큰 그림을 자동 생성하고 디자인 핵심만 손배치한다.

비기능 목표:
- 디자이너 관점에서 "예쁜" 원형 섬 — 시선축, 클러스터링, 좌우 비대칭
- 재현성 확보 — `MapGenerator` 메뉴로 언제든 재생성 가능
- 게임 메커니즘 호환 — 4인 협동, 한 세션 15~30분, 3영역 진행

---

## 2. 결정 사항 (브레인스토밍 결과)

| 항목 | 결정 |
|---|---|
| 지형 솔루션 | Synty Slope/Hill 메쉬 + NavMesh 베이크 (Max Slope 60°) |
| 배치 방식 | **하이브리드 C** — 코드 자동 + Claude MCP 손다듬기 |
| 맵 형태 | **원형 섬** (지름 150u) |
| 영역 비율 | 해안 25% / 숲 35% / 산지 25% / 외곽 15% |
| 슬로프 비중 | 해안 0%, 숲 15%, 산지 50% (전체 약 20%) |
| 외곽 처리 | **혼합형** — 남(모래+얕은물), 동/서(모래+절벽+물), 북(절벽+깊은물+산) |
| Skydome | 적용 (Cloud 4~5개, y ≥ 30) |
| 보조 차단 | 보이지 않는 원형 벽 (반경 75, 높이 5, 두께 2) |
| 마우스 시점 | `Plane` → `Physics.Raycast` (Ground 레이어) |
| Ground 레이어 | 신규 추가, Synty 메쉬에 일괄 적용 |
| `SampleEnvironment` | 카탈로그로 보존, 새 맵은 `MapRoot/GeneratedMap`에 생성 |

---

## 3. 아키텍처

```
┌──────────────────────────────────────────┐
│ 1. MapGenerator 자동 (코드)               │  ← 큰 그림
│    - 원형 섬 격자 + 영역 규칙             │
│    - 슬로프/언덕/데코 자동 배치           │
│    - 외곽 처리, Skydome, 보조 콜라이더    │
└──────────────────┬───────────────────────┘
                   ▼
┌──────────────────────────────────────────┐
│ 2. NavMesh 베이크 (코드, Max Slope 60°)   │
└──────────────────┬───────────────────────┘
                   ▼
┌──────────────────────────────────────────┐
│ 3. Claude MCP 손다듬기 (수동)             │  ← 디자인 핵심
│    - 랜드마크 위치/스케일                  │
│    - 등산로 슬로프 곡선                    │
│    - NavMesh 끊김 보정                     │
└──────────────────┬───────────────────────┘
                   ▼
┌──────────────────────────────────────────┐
│ 4. NavMesh 재베이크 (손다듬기 반영)       │
└──────────────────┬───────────────────────┘
                   ▼
┌──────────────────────────────────────────┐
│ 5. PlayerCharacter Raycast (코드)         │
└──────────────────┬───────────────────────┘
                   ▼
┌──────────────────────────────────────────┐
│ 6. QA (TestManager 시나리오)              │
└──────────────────────────────────────────┘
```

---

## 4. 컴포넌트별 상세

### 4-1. MapGenerator 개선

**위치**: [Assets/Scripts/Editor/MapGenerator.cs](../WES/Assets/Scripts/Editor/MapGenerator.cs)

**입력 상수**:
- `ISLAND_RADIUS = 75f` — 섬 외곽 반경
- `PLAYABLE_RADIUS = 70f` — NavMesh 유효 반경
- `GROUND_TILE_SIZE = 4.5f` — 기존 유지
- `MAX_SLOPE_DEGREE = 60f` — NavMesh 베이크용

**프리팹 카테고리** — `SampleEnvironment` 풀에서 분류:
| 카테고리 | 프리팹 패턴 | 용도 |
|---|---|---|
| GroundFlat | `SM_Gen_Env_Ground_Grass_*`, `SM_Gen_Env_Ground_Dirt_*` | 평탄 타일 |
| GroundSlope | `SM_Gen_Env_Ground_Slope_*` (4종) | 경사 타일 |
| Hill | `SM_Gen_Env_Hill_*` (3종) | 언덕 |
| Mountain | `SM_Gen_Env_Mountain_*` (3종) | 산 배경 |
| Tree | `SM_Gen_Env_Tree_*`, `SM_Gen_Env_Tree_Pine_*`, `SM_Gen_Env_Tree_Dead_*` | 나무 |
| Bush | `SM_Gen_Env_Bush_*`, `SM_Gen_Env_Shrub_*` | 덤불 |
| Rock | `SM_Gen_Env_Rock_*`, `SM_Gen_Env_Rock_Pebbles_*` | 바위 |
| Grass | `SM_Gen_Env_Grass_*`, `SM_Gen_Env_Grass_Tall_*`, `SM_Gen_Env_Fern_*` | 풀 |
| Flower | `SM_Gen_Env_Flowers_*` | 꽃 |
| Mushroom | `SM_Gen_Env_Mushroom_*` | 버섯 |
| Stump | `SM_Gen_Env_Stump_*` | 그루터기 |
| Cliff | `SM_Gen_Env_Cliff_*` | 절벽 (외곽 처리용) |
| Skydome | `SM_Gen_Env_Skydome_*` | 하늘돔 |
| Cloud | `SM_Gen_Env_Cloud_*` | 구름 |
| Water | `SM_Gen_Env_Water_Plane_*` | 물 평면 |

**영역 정의** (z 좌표 기준):
| 영역 | z 범위 | 슬로프 비중 | 핵심 메쉬 |
|---|---|---|---|
| 해안 | -75 ~ -10 | 0% | GroundFlat (Grass) + 모래/조개 |
| 숲 | -10 ~ +30 | 15% | GroundFlat + GroundSlope (드문) + Hill 1~2개 + Tree 클러스터 |
| 산지 | +30 ~ +75 | 50% | GroundSlope (빽빽) + Hill 3~4개 + Mountain 배경 |

**원형 섬 마스킹**:
- 좌표 (x, z)에서 중심까지 거리 ≤ `PLAYABLE_RADIUS` 만 ground 배치
- `PLAYABLE_RADIUS` ~ `ISLAND_RADIUS` 사이는 모래/절벽 영역
- `ISLAND_RADIUS` 바깥은 물 평면

**외곽 처리 (혼합형)**:
- **남쪽 (z < -50)**: 모래 → 얕은 물 (Y=-0.3) — 난파 컨셉
- **동/서 (|x| > 50, -10 < z < 30)**: 모래 + 작은 절벽 + 깊은 물
- **북쪽 (z > 50)**: 절벽 + 깊은 물 + Mountain 배경

**보조 콜라이더**:
- 외곽 원형 벽: 반경 75, 높이 5, 두께 2, 보이지 않는 32~64각형 콜라이더
- `transform.position` 직접 이동 fallback 방어

**디자인 규칙 (코드 박힘)**:
- 클러스터링: 나무는 Poisson disk 샘플링 (최소거리 3u) + 군락 노이즈
- 좌우 비대칭: x축 미러 시 같은 종류 안 두기 (시드 기반)
- 스케일 변형: Tree 0.8~1.4, Rock 0.7~1.6, Bush 0.9~1.3
- 회전 랜덤: y축 0~360°
- 레이캐스트 그라운딩: `GetTerrainHeight(x, z)` 활용 (기존 함수)

**메뉴**:
- `Tools/Map Generator/Generate Island Map` — 전체 재생성 (기존 갱신)
- `Tools/Map Generator/Bake NavMesh` — Max Slope 60° (기존 갱신)
- `Tools/Map Generator/Populate Forest and Mountain` — 부분 재생성 (기존 유지)

**부모 GameObject**:
- 새 맵: `MapRoot/GeneratedMap` (기존 `MapRoot` 자식 정리 후 재생성)
- 카탈로그: `MapRoot/SampleEnvironment` 보존

### 4-2. NavMesh 베이크

- **Max Slope: 45° → 60°**
- **Step Height: 0.4** (작은 턱 무시)
- **Agent Radius/Height**: 기존 값 유지
- 유효 영역(`PLAYABLE_RADIUS` 안쪽)만 `NavigationStatic` 마크
- `NavMeshBuilder.BuildNavMesh()` 자동 호출

### 4-3. Claude MCP 손다듬기 (랜드마크)

자동 생성 결과 확인 후 **3~5개 포인트만** 미세 조정:

| 포인트 | 작업 | MCP 도구 |
|---|---|---|
| 산지 정상 | `Mountain_02` (0, 0, 65), 스케일 2.5x — 시작점 정면 | `u_set_transform` |
| 등산로 | 슬로프 2~3개 곡선 연결 (숲→산지) | `u_set_transform` |
| 숲 분위기 | 그루터기 + 버섯 군락 (중앙 cluster) | `u_editor_gameobject add` |
| Cloud 배치 | 4~5개 메쉬, y ≥ 30, x/z 분산 | `u_set_transform` |
| NavMesh 끊김 | 베이크 후 끊긴 곳 슬로프 추가/이동 | `u_set_transform` |

### 4-4. PlayerCharacter 마우스 Raycast

**위치**: [Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs:350](../WES/Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs#L350)

**변경 전** (현재):
```csharp
Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
if (groundPlane.Raycast(ray, out float distance)) { ... }
```

**변경 후**:
```csharp
Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
if (Physics.Raycast(ray, out RaycastHit hit, 200f, m_GroundLayerMask)) {
    Vector3 mouseWorldPos = hit.point;
    ...
}
```

- `[SerializeField] private LayerMask m_GroundLayerMask;` 추가
- Inspector에서 Ground 레이어 선택

**Ground 레이어 신규**:
- `ProjectSettings/TagManager.asset`에 "Ground" 레이어 등록
- `MapGenerator`가 생성한 모든 ground/slope/hill/mountain GameObject에 일괄 적용
- 기존 데코(나무, 풀)에는 적용하지 않음 (마우스가 데코를 가리키지 않게)

### 4-5. QA (TestManager 시나리오)

| 시나리오 | 검증 항목 |
|---|---|
| 슬로프 위 플레이어 이동 | Y가 자연 보정되는지 (NavMeshAgent.Move) |
| 몬스터 슬로프 추적 | NavMeshAgent.SetDestination이 슬로프 따라 이동 |
| 마우스 정확도 | 경사면에서 클릭 위치가 표면과 일치 |
| 가파른 곳 차단 | >60° 경사 못 올라감 (NavMesh가 안 깔림) |
| 외곽 차단 | 보조 콜라이더로 섬 밖 못 나감 |
| 다양한 높이 | StartPosition / EscapePoint 사이 자연 등반 |

---

## 5. 영향 파일

| 파일 | 변경 유형 |
|---|---|
| [Assets/Scripts/Editor/MapGenerator.cs](../WES/Assets/Scripts/Editor/MapGenerator.cs) | 큰 폭 개선 (원형 섬, 영역 규칙, 외곽, Skydome, 콜라이더) |
| [Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs](../WES/Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs) | `UpdateMouseLook` Raycast 변경, `m_GroundLayerMask` 추가 |
| `ProjectSettings/TagManager.asset` | Ground 레이어 신규 |
| `Assets/Scenes/Ingame.unity` | 새 맵, 손다듬기 결과, Skydome |
| NavMesh 데이터 (asset) | 재베이크 산출물 |

**미수정 파일** (NavMeshAgent가 자동 처리):
- [Assets/Scripts/WorldBaseObject/CharacterBase.cs](../WES/Assets/Scripts/WorldBaseObject/CharacterBase.cs)
- [Assets/Scripts/WorldBaseObject/Monster/MonsterStateMachine.cs](../WES/Assets/Scripts/WorldBaseObject/Monster/MonsterStateMachine.cs)

---

## 6. 핵심 좌표

| 오브젝트 | 좌표 | 비고 |
|---|---|---|
| StartPosition1 | (-3, 0, -42) | 핸드오프 그대로 |
| StartPosition2 | (3, 0, -42) | 핸드오프 그대로 |
| StartPosition3 | (-6, 0, -38) | 핸드오프 그대로 |
| StartPosition4 | (6, 0, -38) | 핸드오프 그대로 |
| EscapePoint | (0, 8, 60) | 산정상에 약간 높이 둠 |
| Mountain_02 (랜드마크) | (0, 0, 65) | 스케일 2.5x |
| Area1_Beach | (0, 0, -35) | 핸드오프 그대로 |
| Area2_Forest | (0, 0, 10) | 약간 조정 |
| Area3_Mountain | (0, 0, 50) | 약간 조정 |

---

## 7. 구현 순서 — 구간 분할 + 체크포인트

> **원칙**: 한 번에 전체를 끝내지 않고 구간별로 끊어 진행한다. 각 구간 끝에 사용자 확인 게이트를 둔다.

### 구간 1 — 골격 인프라 (코드 + 레이어)
- `MapGenerator` 카테고리 분류 + 프리팹 풀 헬퍼 추가
- 원형 섬 마스킹 함수 (`IsInsideIsland`, `GetRegion`)
- Ground 레이어 등록 (TagManager)
- **체크포인트 1**: 기존 메뉴 깨지지 않고 컴파일 성공 + 헬퍼 단위 테스트(있다면) 통과 → 사용자 보고
- **커밋 1**: "MapGenerator 카테고리 분류 + 원형 섬 마스킹 헬퍼 추가"

### 구간 2 — 자동 맵 생성 코어
- 영역별 ground/slope/hill 자동 배치 (해안/숲/산지)
- 외곽 처리 (혼합형: 모래/절벽/물)
- 데코 자동 배치 (Tree/Bush/Rock/Grass/Flower)
- Skydome + Cloud 배치
- 보조 원형 콜라이더 생성
- **체크포인트 2**: `Tools/Map Generator/Generate Island Map` 실행 → 씬뷰 스크린샷으로 결과 확인 → 사용자 OK
- **커밋 2**: "MapGenerator 원형 섬 자동 생성 + 외곽/Skydome 적용"

### 구간 3 — NavMesh 베이크 (자동)
- `BakeNavMesh` 메뉴에 Max Slope 60° / Step Height 0.4 적용
- `PLAYABLE_RADIUS` 안쪽만 NavigationStatic 마크
- **체크포인트 3**: 베이크 실행 → 씬뷰에서 NavMesh 시각 확인 → 끊긴 부분 식별
- **커밋 3**: "NavMesh 베이크 Max Slope 60° 적용"

### 구간 4 — Claude MCP 손다듬기 (수동)
- 4-1. Mountain_02 랜드마크 (0, 0, 65), 스케일 2.5x
- 4-2. 등산로 슬로프 곡선 연결
- 4-3. 숲 그루터기 + 버섯 군락
- 4-4. Cloud 메쉬 4~5개 분산 배치
- 4-5. NavMesh 끊김 보정 (필요 시 슬로프 추가/이동)
- 각 작업 후 스크린샷으로 시각 확인
- **체크포인트 4**: 5개 포인트 손다듬기 완료 후 NavMesh 재베이크 → 사용자 분위기 OK
- **커밋 4**: "맵 랜드마크 손배치 + NavMesh 재베이크"

### 구간 5 — PlayerCharacter 마우스 Raycast
- `m_GroundLayerMask` SerializeField 추가
- `UpdateMouseLook()` Raycast로 변경
- Inspector에서 Ground 레이어 매핑
- **체크포인트 5**: 평지/경사면에서 마우스 클릭 위치 정확도 수동 확인 → 사용자 OK
- **커밋 5**: "PlayerCharacter 마우스 시점을 Physics.Raycast로 전환"

### 구간 6 — QA 시나리오 검증
- TestManager 시나리오 6개 작성 (5절 표 참조)
- `dev-qa` 스킬로 플레이모드 자동 검증
- 실패 시 해당 구간으로 돌아가 수정
- **체크포인트 6**: 모든 시나리오 PASS → 사용자 최종 OK
- **커밋 6**: "지형 경사 이동 QA 시나리오 통과"

### 진행 규칙
- 한 구간 완료 전까지 다음 구간 시작 안 함
- 각 체크포인트에서 사용자 확인 없이 자동으로 다음 구간 진행 안 함
- 코드 에러/런타임 에러는 자동 수정 후 재시도, 단 기획적 판단 필요 시 보고
- 구간이 너무 커지면 추가 분할 (예: 구간 2에서 영역별 배치를 4-2-1, 4-2-2로 더 쪼갤 수 있음)

---

## 8. 미결정 / 후속 과제 (M3 이후)

- 동적 시드 시스템 (매 게임마다 다른 섬)
- 날씨/시간대 변화 (해안 안개, 산지 눈)
- 미니맵
- 카메라 워커가 경사 따라 자연스럽게 추적되는지 후속 검증
- 200×200u 확장 (콘텐츠 충분해지면)
