# Scene Review 스킬 설계서

> **작성일**: 2026-04-23 (수정: 2026-04-26)
> **목적**: 3D 씬 배치가 디자이너 관점에서 자연스럽고 미적으로 괜찮은지 검수하는 전담 스킬. 물리적 겹침/관통은 그 자체로 문제가 아니며, 의도된 연출일 수 있다. 수정은 하지 않는다.

---

## 1. 개요

### 역할
- 씬 배치의 **미적 품질** 검수 (디자이너 관점)
- 기능적 장애와 시각적 어색함을 가려냄
- **UI는 대상 아님** (UI는 `ui-review` 스킬이 담당)
- **리포트만 출력** — 코드/프리팹/씬 수정은 절대 하지 않음

### 호출 방식
- `/scene-review` — 기본 스코프 검수 (씬 전체, 후보 상한 적용)
- `/scene-review <GameObject명>` — 해당 오브젝트와 주변 검수
- `/scene-review --full` — 후보 상한 없이 전수 검수

### 사용 시점
- 맵 배치 작업 직후 1차 검수 (에디터 모드)
- QA 단계 2차 검수 (플레이 모드, 동적 상태)
- 다른 세션 작업물 단독 검수

### ui-review와의 차이

| 항목 | ui-review | scene-review |
|---|---|---|
| 대상 | Canvas, HUD, Popup | 3D GameObject (맵/몬스터/자원) |
| 주 판정 | UI 레이아웃 정합 | 디자이너 관점 미적/구도 판단 |
| 데이터 기반 | 거의 없음 | **Scene Graph + 룰 엔진 위주** |
| 모드 | 선택 | **에디터 모드 기본**, 동적 검수만 플레이 |
| 최종 근거 | 스크린샷 | 룰 + 사용자 리뷰 (스크린샷은 첨부) |

---

## 2. 판정 원칙 (핵심)

> 이 원칙이 나머지 모든 섹션보다 우선한다.

1. **의도된 연출 존중**
   - 오브젝트 겹침/관통/근접이 보여도 시각적으로 어색하지 않으면 통과
   - 예: 바위가 바위 위에 살짝 걸쳐 자연스러운 군락

2. **기능 오브젝트는 예외 — 즉시 플래그**
   - **Player 스폰 / 몬스터 스폰 / 상호작용 오브젝트 / 경로**가 덮이거나 접근 불가 상태면 자동 "문제"
   - 판정 기준: 기능 수행 가능 여부

3. **데이터/룰이 1차 근거, 스크린샷은 사용자 리뷰용**
   - 가능한 한 데이터(Scene Graph + Physics)와 룰 엔진으로 자동 판정
   - LLM 시각 판정은 최소화 (신뢰 한계 + 토큰 비용)
   - 주관 항목(구도/색감/실루엣/라이팅)은 **사용자가 직접 봄** — 스킬은 스크린샷 첨부만

4. **정량 임계값은 후보 필터**
   - 지면 이격 0.5m, 침투 깊이 0.4m 등은 "확인 필요" 표식
   - 자동 실패가 아니라 추가 검토 대상

---

## 3. 체크리스트

체크리스트는 두 계층으로 나뉜다.

### A. 기능 장애 (자동 플래그)

| # | 항목 | 판정 방법 |
|---|---|---|
| A1 | Player 스폰 덮임 | Player 스폰 좌표가 다른 오브젝트 OBB 내부 |
| A2 | 몬스터 스폰 덮임 | SpawnPoint 좌표가 OBB 내부 → 스폰 실패 가능 |
| A3 | 접근 불가 | NavMesh.SamplePosition 실패 (스폰 좌표 도달 불가) |
| A4 | 상호작용 차단 | 상호작용 오브젝트가 다른 오브젝트에 완전히 가려짐 |

### B. 미적 우려 (사용자 리뷰 필수)

| # | 카테고리 | 트리거 조건 | 판정 |
|---|---|---|---|
| B1 | 부자연 관통 | OBB 간 침투 > 0.3m | 명백시 자동 플래그(>1m), 경계는 사용자 |
| B2 | 공중 뜸 | Raycast 지면 이격 > 0.5m + 경사 < 10° | 명백시 자동(>2m), 경계는 사용자 |
| B5 | 스케일 부조화 | Player 대비 비율 > 10× 또는 < 0.1× | 사용자 |
| B6 | 색감/톤 충돌 | 자동 판정 불가 | 사용자 |
| B7 | 실루엣 붕괴 | 자동 판정 불가 | 사용자 |
| B8 | 반복감 | 같은 prefab N개 이상 반경 내 군집 | 후보 표시, 사용자 |
| B9 | 핑크 메쉬 | Material.shader == default | **자동 플래그** |
| B10 | 텍스처 누락 | Material.mainTexture == null | **자동 플래그** |
| B11 | 라이팅 이상 | 자동 판정 불가 | 사용자 |
| B12 | 애니메이션 정지 | Animator.GetCurrentAnimatorStateInfo == "T-Pose" | **자동 플래그** (플레이 모드) |
| B13 | 구도 공허/과밀 | 반경 N미터 내 오브젝트 수가 임계값 외 | 후보 표시, 사용자 |

### 1차 (에디터) vs 2차 (플레이)

| | 1차 — 에디터 모드 | 2차 — 플레이 모드 |
|---|---|---|
| 대상 | A1~A4, B1, B2, B5, B6, B7, B8, B9, B10, B11, B13 | B12, 동적 스폰 오브젝트 |
| 데이터 | Scene Graph (정적) | 런타임 상태 (Animator, 동적 오브젝트) |
| 사용 시점 | 맵 배치 작업 후 | QA 통합 검증 |

---

## 4. 검증 방법 — 3-Layer 구조

### Layer 1 — Scene Graph 로드 + 로컬 공간 인덱스

스킬 시작 시 1회 수행. 이후 모든 공간 쿼리는 로컬 메모리에서 처리 (MCP 왕복 없음).

```
1. document/scene-graph/<sceneName>.json 로드
2. JSON 파싱 → Scene Graph 메모리 객체
3. 옥트리(또는 그리드) 공간 인덱스 구축
4. 카테고리별 오브젝트 분류 (기능 오브젝트, 환경, 몬스터 등)
```

**파일이 씬보다 오래된 경우**: 사용자에게 경고 + `SceneGraphExporter` 재실행 안내.

### Layer 2 — 자동 판정 + 룰 엔진

룰 엔진이 우선 판정, 명백 케이스는 LLM 없이 즉시 결과 산출.

**자동 플래그 룰**:
- A1~A4: Point-in-OBB / NavMesh 판정 → bool
- B9, B10: Material 검사 → bool
- B1 명백 (침투 > 1m): 자동 플래그
- B2 명백 (공중 > 2m + 평지): 자동 플래그
- B12: Animator 상태 검사 (플레이 모드)

**룰 엔진 예 (의도 연출 인식)**:
```
- vegetation + rock + 침투 < 0.3m + 같은 biome → OK (자연스러운 숲)
- tree + building + 침투 > 0m → FLAG (관통 버그)
- rock + slope > 15° + gap < 0.5m → OK (경사면 자연 정착)
```

**경계 케이스**: 룰로 판정 안 되면 후보 목록에 올림 → Layer 3.

### Layer 3 — 스크린샷 (사용자 리뷰용)

LLM 판정에 의존하지 **않음**. 후보 영역의 스크린샷을 찍어 리포트에 첨부, 사용자가 직접 확인.

**촬영 대상**:
- B 카테고리 후보 (룰 엔진이 명확히 판정 못 한 케이스)
- 주관 항목 (B6, B7, B11)이 의심되는 구역

**Tier 전략**:
- **Tier 1 (기본)**: 후보당 1장 (탑다운 또는 적절한 시점)
- **Tier 2 (요청 시)**: 사용자가 추가 각도 요청 시 멀티뷰 (탑다운 + 플레이어 시점 + 옆면)

**카메라 배치 (경사 지형 대응)**:
- 카메라 Y = `max(target.y, terrainHeight) + 오프셋`
- 옆면 뷰는 해당 좌표의 지형 높이 위에서 촬영

---

## 5. Scene Graph 데이터 포맷

### 파일 위치
- `Assets/Editor/SceneGraphs/<sceneName>.json`

### 엔트리 구조 (필드 5개)

```json
{
  "name":      "SM_Gen_Env_Tree_02",
  "hierarchy": "/Map/Forest/SM_Gen_Env_Tree_02",
  "position":  [88.1, 0.5, 23.4],
  "rotation":  [0.0, 0.2588, 0.0, 0.9659],
  "obb": {
    "min": [-0.3, 0.0, -0.7],
    "max": [0.7, 5.8, 0.7]
  }
}
```

### 필드 정의

| 필드 | 의미 | 기준 |
|---|---|---|
| `name` | GameObject.name | — |
| `hierarchy` | 루트부터의 전체 경로 | `/Root/Parent/Self` |
| `position` | `transform.position` (pivot) | 월드 좌표 |
| `rotation` | `transform.rotation` 쿼터니언 | 월드 회전 (x, y, z, w) |
| `obb.min` | 메쉬 로컬 AABB의 min × lossyScale | pivot 기준 로컬 좌표 |
| `obb.max` | 메쉬 로컬 AABB의 max × lossyScale | pivot 기준 로컬 좌표 |

### OBB 비대칭 처리

`obb.min`, `obb.max`는 pivot 기준 로컬 좌표. 비대칭 메쉬도 정확히 표현됨.

**예 — 나무 (pivot=밑동)**:
- `obb.min.y = 0.0` (밑동이 pivot 위치)
- `obb.max.y = 5.8` (꼭대기 5.8m 위)

**예 — 대칭 바위 (pivot=중심)**:
- `obb.min = (-0.5, -0.5, -0.5)`
- `obb.max = (0.5, 0.5, 0.5)`

### OBB 사용 (월드 변환)

**8 corner 월드 좌표**:
```
for (sx, sy, sz) in {min.x, max.x} × {min.y, max.y} × {min.z, max.z}:
    localCorner = (sx, sy, sz)
    worldCorner = rotation * localCorner + position
```

**Point-in-OBB**:
```
localPoint = inverseRotation * (worldPoint - position)
inside = (min.x <= localPoint.x <= max.x)
      && (min.y <= localPoint.y <= max.y)
      && (min.z <= localPoint.z <= max.z)
```

회전, 비대칭, 스케일 모두 정확히 반영.

### Scene Graph 산출물 전체 구조

```json
{
  "scene":      "Ingame",
  "exportedAt": "2026-04-23T14:32:11",
  "objects": [
    { "name": "...", "hierarchy": "...", "position": [...], "rotation": [...], "obb": { ... } },
    ...
  ]
}
```

---

## 6. 기능 오브젝트 식별 규칙

A 카테고리(기능 장애) 자동 판정에 필요한 식별 규칙. WES 코드 구조에 맞춰 정의.

| 카테고리 | 식별 방법 |
|---|---|
| Player 스폰 위치 | `PlayerSpawnPoint` 컴포넌트 보유 GameObject (또는 `PlayerSpawn` 태그) |
| 몬스터 스폰 | `SpawnPoint` 컴포넌트 보유 |
| 상호작용 오브젝트 | `IInteractable` 인터페이스 구현 (또는 `Interactable` 태그) |
| 경로 / 도달 영역 | NavMesh 베이크 영역 |

→ 위 식별 규칙은 **선행 조건**이다. WES 실제 컴포넌트/태그 구조에 맞춰 구체화 필요 (구현 단계에서 확정).

---

## 7. 검수 스코프 기본값

씬이 크면 후보 폭발 우려 → 기본 스코프와 후보 상한 적용.

### 인자 없이 호출 시
- `/scene-review` → **씬 전체** 대상
- 후보 상한: **15건**
- 초과 시 우선순위 (A 카테고리 > B 명백 > B 경계) 상위 15건만 리포트, 나머지는 좌표 목록만 첨부

### 명시 옵션
- `/scene-review <GameObject명>` → 해당 오브젝트 + 반경 30m
- `/scene-review --full` → 상한 없이 전수
- `/scene-review --max=30` → 상한 변경

---

## 8. 실행 흐름

```
1. 사전 조건 검증
   ├── Scene Graph 파일 존재? 씬보다 최신?
   ├── NavMesh 베이크 상태?
   ├── 기능 오브젝트(Player 스폰/SpawnPoint) 존재?
   └── 실패 시 사용자에게 안내 후 중단

2. Layer 1: Scene Graph 로드
   ├── JSON 파싱
   └── 공간 인덱스 구축

3. Layer 2: 자동 판정 + 룰 엔진
   ├── A 카테고리 전수 검사 → 자동 플래그
   ├── B 카테고리 명백 케이스 → 자동 플래그
   ├── B 카테고리 경계 케이스 → 후보 목록
   └── 후보 상한 적용

4. (옵션) 플레이 모드 진입 — 2차 검수 시만
   ├── u_play_control(enter)
   ├── B12 (애니메이션) 검사
   ├── 동적 스폰 오브젝트 재검수
   └── u_play_control(exit)

5. Layer 3: 스크린샷 (Tier 1)
   ├── 후보 좌표로 카메라 이동 (지형 높이 보정)
   └── u_screenshot 1장 / 후보

6. 리포트 작성
   ├── 자동 플래그 결과
   ├── 사용자 리뷰 항목 (스크린샷 첨부)
   └── 통계/요약

7. 파일 저장
   ├── document/scene-review/<timestamp>.md
   └── document/scene-review/screenshots/<timestamp>/*.png
```

---

## 9. 토큰 비용 / Tier 전략

### 후보당 비용 추정 (Tier 1 기본)
| 구성 | 토큰 |
|---|---|
| 스크린샷 1장 (640×640) | ~600 |
| 메타데이터 텍스트 | ~300 |
| 룰 엔진 결과 + 후보 정보 | ~200 |
| **합계** | **~1,100 / 후보** |

### 씬 전체 검수 (15후보 상한)
- 입력: ~17K tokens
- 비용: **~$0.25** (Opus 기준)

### Tier 2 (사용자가 추가 각도 요청 시)
- 후보당 +1500 tokens (멀티뷰 추가)
- 명시적 요청 시만

### LLM 호출 최소화 정책
- **A 카테고리는 LLM 호출 안 함** (코드/룰만)
- B 명백 케이스도 LLM 호출 안 함
- 경계 케이스는 LLM 판정 대신 **사용자에게 스크린샷+근거 제시**가 기본
- LLM 호출은 **사용자가 명시적으로 요청 시만** (예: `/scene-review --llm-judge`)

---

## 10. 리포트 출력

### 파일 위치
- 리포트: `document/scene-review/<YYYY-MM-DD-HHMM>.md`
- 스크린샷: `document/scene-review/screenshots/<YYYY-MM-DD-HHMM>/<오브젝트명>.png`

### 형식 예시

```markdown
## Scene Review — Ingame 씬 (2026-04-23 18:30)

**스코프**: 씬 전체 (오브젝트 487개)
**모드**: 에디터 모드
**후보 수**: 6 (상한 15 내)
**Scene Graph**: Assets/Editor/SceneGraphs/Ingame.json (2026-04-23 18:25 갱신)

### 🚨 자동 플래그 (3건)
1. [A1] Player 스폰 덮임
   - PlayerSpawnPoint @(12.3, 0, 45.6)이 SM_Gen_Env_Hill_04 OBB 내부
2. [A3] 접근 불가
   - SpawnPoint_Wolf_01 @(34.5, 8.2, 67.8) NavMesh 도달 불가
3. [B9] 핑크 메쉬
   - SM_Building_03: Material 누락 (default shader)

### ⚠️ 룰 엔진 판정 (1건)
1. [B1] 부자연 관통
   - SM_Tree_05 침투 깊이 1.2m → 명백 관통 (자동 플래그)

### 📷 사용자 리뷰 필요 (2건)
1. [B6] 색감 의심 — 북쪽 해안가 영역
   - 스크린샷: screenshots/2026-04-23-1830/north_coast.png
2. [B13] 구도 — 중앙 평원 공허 의심
   - 반경 50m 내 오브젝트 2개만
   - 스크린샷: screenshots/2026-04-23-1830/center_plain.png

### 📋 요약
- 자동 플래그: 4건 (즉시 수정 권장)
- 사용자 리뷰: 2건
- 토큰: ~14K
- 비용: ~$0.20
```

---

## 11. 선행 조건

### 11-1. SceneGraphExporter 구현 (Unity Editor 코드)

**위치**: `Assets/Editor/SceneGraphExporter.cs`

**기능**:
- 씬의 모든 GameObject 순회
- 각 오브젝트의 OBB(min/max) 계산 (mesh.bounds + lossyScale)
- 5필드 JSON 엔트리 생성
- 파일 출력 → `Assets/Editor/SceneGraphs/<sceneName>.json`

**트리거**:
- MenuItem: `Tools/Scene Review/Export Scene Graph` (수동)
- `EditorSceneManager.sceneSaved` 훅 (자동)

**자식 메쉬 처리**: 자식 GameObject도 개별 엔트리로 출력. hierarchy 경로로 부모-자식 관계 표현.

**SkinnedMeshRenderer 처리**: `BakeMesh()`로 현재 포즈 기준 메쉬 추출 후 OBB 계산. 에디터 모드에서는 T-포즈 기준으로 일관됨.

### 11-2. 기능 오브젝트 식별 컴포넌트
- `PlayerSpawnPoint`, `SpawnPoint`, `IInteractable` 등이 WES 코드에 정의되어 있어야 함
- 미존재 시 구현 필요 (별도 작업)

### 11-3. NavMesh 베이크
- 검수 시 NavMesh가 베이크된 상태여야 A3 검사 가능

### 11-4. MCP 도구 확인
- `u_editor_scene` (씬 카메라 정보), `u_screenshot`, `u_console`, `u_editor_gameobject` 동작 확인
- 에디터 모드에서 static 메서드 호출 가능 여부 확인 (필요 시 별도 MCP 도구 추가 검토)

---

## 12. 제약사항

- **수정 금지**: 코드, 프리팹, 씬 오브젝트 일체 수정 안 함
- **리포트 전용**: 발견한 문제를 정리해서 보고만
- **UI 검수 제외**: Canvas/HUD/Popup은 ui-review로
- **자동 실패 범위 한정**: A 카테고리 + B 명백 케이스만 자동 플래그. 나머지는 사용자 리뷰
- **의도된 연출 플래그 금지**: 룰 엔진이 "자연스러운 케이스"로 판정 시 플래그 안 함
- **LLM 판정은 옵션**: 기본 흐름은 LLM 호출 없음. `--llm-judge` 옵션 시만 호출

---

## 13. 향후 확장

- **PCA 기반 Tight OBB**: 정점 분포 분석으로 메쉬 형상에 더 정확한 OBB 계산
- **의도 연출 화이트리스트**: `scene-intent.yaml`에 "이 좌표의 겹침은 의도"라고 명시 시 스킵
- **Regression Diff**: 이전 검수 결과와 비교, 새로 생긴 문제 강조
- **시간대별 라이팅 비교**: 주/야 모드별 스크린샷 자동 비교
- **카메라 궤도 자동 촬영**: 동선 따라 연속 스크린샷
- **증분 Scene Graph**: 변경된 오브젝트만 갱신 (대규모 씬에서)
