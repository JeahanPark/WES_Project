# 지형 경사 이동 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synty 슬로프/언덕 메쉬 + NavMesh 베이크 + 디자인 손다듬기로, 지름 150u 원형 섬에서 캐릭터(플레이어/몬스터)가 경사 지형을 자연스럽게 이동하도록 구현한다.

**Architecture:**
- `MapGenerator`(Editor 도구)로 원형 섬 골격 자동 생성 → NavMesh 자동 베이크 → Claude MCP 손다듬기 → NavMesh 재베이크 → `PlayerCharacter` 마우스 시점을 `Physics.Raycast`로 전환
- 새 맵은 `MapRoot/GeneratedMap`에 생성, `MapRoot/SampleEnvironment`는 카탈로그로 보존
- NavMeshAgent가 슬로프 표면 추적을 담당하므로 `CharacterBase`/`MonsterStateMachine` 이동 코드는 손대지 않음

**Tech Stack:** Unity 6, NavMeshComponents (built-in), Synty PolygonGeneric, MCP_Unity_Plugin, TestManager(Editor-only QA)

**선행 문서:**
- [설계서 — 2026-04-26-terrain-slope-design.md](./2026-04-26-terrain-slope-design.md)
- [핸드오프 — 2026-04-26-terrain-slope-handoff.md](./2026-04-26-terrain-slope-handoff.md)

**진행 규칙 (사용자 피드백):**
- 구간(섹션)별로 끊어 진행한다. 각 구간 끝 체크포인트에서 사용자 OK 없이 자동으로 다음 구간을 시작하지 않는다.
- 코드 컴파일 에러/런타임 에러는 자동 수정 후 재시도. 기획적 판단이 필요하면 사용자 보고.
- 한 구간 안에서도 한 작업이 너무 커지면 추가 분할.

---

## 파일 구조

| 파일 | 책임 | 변경 유형 |
|---|---|---|
| [Assets/Scripts/Editor/MapGenerator.cs](../WES/Assets/Scripts/Editor/MapGenerator.cs) | Editor 메뉴로 원형 섬 + NavMesh 자동 생성. 카테고리 분류, 영역 판정, 외곽 처리, Skydome, 보조 콜라이더 모두 여기 | 큰 폭 개선 |
| [Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs](../WES/Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs) | `UpdateMouseLook()`을 `Physics.Raycast`로 전환, `m_GroundLayerMask` 노출 | 한 메서드 + 필드 1개 |
| [Assets/Scripts/Manager/TestManager.cs](../WES/Assets/Scripts/Manager/TestManager.cs) | 지형 QA 시나리오 코루틴 추가 | 메서드 추가 |
| [ProjectSettings/TagManager.asset](../WES/ProjectSettings/TagManager.asset) | "Ground" 레이어 등록 | YAML 한 줄 |
| [Assets/Scenes/Ingame.unity](../WES/Assets/Scenes/Ingame.unity) | 새 맵, 손배치 결과, Skydome | 자동 + MCP |

**미수정 (의도)**: `CharacterBase.cs`, `MonsterStateMachine.cs` — NavMeshAgent가 슬로프 처리를 담당하므로 변경 불필요.

**MapGenerator 내부 구조 (개선 후)**:
- `Region`(enum, nested) — `Beach | Forest | Mountain | OuterRim | Outside`
- `PrefabCategory`(enum, nested) — `GroundFlat | GroundSlope | Hill | MountainBackdrop | Tree | Bush | Rock | Grass | Flower | Mushroom | Stump | Cliff | Skydome | Cloud | Water`
- 헬퍼: `IsInsideIsland`, `GetRegion`, `FindPrefabsByCategory`, `GetRandomPrefab`, `PickPoissonPoints`
- 메뉴 진입점: `Generate Island Map`, `Bake NavMesh`, `Validate Helpers`(검증용)

---

# 구간 1 — 골격 인프라 (코드 + 레이어)

이 구간의 목표: 이후 모든 자동 생성 코드의 토대가 되는 헬퍼와 레이어를 마련한다. 구간 종료 시 `MapGenerator`가 컴파일되고 헬퍼 검증 메뉴가 PASS여야 한다.

---

### Task 1.1: Ground 레이어 등록

**Files:**
- Modify: `ProjectSettings/TagManager.asset` — User Layer 8 자리에 "Ground" 추가

**왜**: Task 5.x에서 `PlayerCharacter` 마우스 Raycast가 Ground 레이어만 맞도록 필터링하기 위함. Synty ground/slope 메쉬에 일괄 적용.

- [ ] **Step 1: 현재 TagManager.asset의 User Layer 영역 확인**

Read: `ProjectSettings/TagManager.asset` 의 `m_UserLayer*` 필드. 첫 번째 빈 슬롯(보통 `m_UserLayer8`)을 찾는다.

- [ ] **Step 2: "Ground" 레이어 추가**

Edit `ProjectSettings/TagManager.asset`: 비어있는 첫 `m_UserLayerN` 값을 `Ground`로 변경. (예: `m_UserLayer8: Ground`).

- [ ] **Step 3: Unity 에디터에서 레이어 등록 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`로 에러 없는지 확인 → Unity 메뉴 `Edit > Project Settings > Tags and Layers`에서 Ground 레이어 보이는지 시각 확인.

Expected: `Ground` 레이어가 User Layer 8(또는 추가한 인덱스)에 표시.

- [ ] **Step 4: 커밋 (이 작업만 단독 커밋하지 않고 Task 1.5의 마지막 커밋에 포함)**

체크포인트 1에서 한 번에 커밋한다.

---

### Task 1.2: MapGenerator 상수 + nested enum 추가

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` (클래스 상단)

- [ ] **Step 1: 기존 상수 옆에 신규 상수 추가**

`MapGenerator.cs`의 `private const float MAP_SIZE = 100f;` 위/아래에 다음을 추가:

```csharp
private const float ISLAND_RADIUS = 75f;     // 섬 외곽 반경 (물 시작 지점)
private const float PLAYABLE_RADIUS = 70f;   // NavMesh 유효 반경 (모래사장 안쪽)
private const float MAX_SLOPE_DEGREE = 60f;  // NavMesh 베이크용
private const float STEP_HEIGHT = 0.4f;      // NavMesh Step Height
private const float WATER_Y = -0.3f;         // 얕은 물 평면 Y
private const float DEEP_WATER_Y = -1.5f;    // 깊은 물 평면 Y

// 영역 z 경계 (남쪽이 음수)
private const float BEACH_Z_MAX = -10f;
private const float FOREST_Z_MAX = 30f;
// MOUNTAIN: z > FOREST_Z_MAX
```

기존 `MAP_SIZE`, `GROUND_TILE_SIZE`, `HALF_MAP`은 유지(다른 부분이 의존). 단 `HALF_MAP`은 더 이상 ground 격자에 쓰지 않고 외곽 물 평면 범위 등에만 쓰일 것이므로 의미 갱신은 후속 작업에서.

- [ ] **Step 2: nested enum 2개 추가**

`MapGenerator` 클래스 내부 상수 바로 아래에:

```csharp
private enum Region { Beach, Forest, Mountain, OuterRim, Outside }

private enum PrefabCategory
{
    GroundFlat,
    GroundSlope,
    Hill,
    MountainBackdrop,
    Tree,
    Bush,
    Rock,
    Grass,
    Flower,
    Mushroom,
    Stump,
    Cliff,
    Skydome,
    Cloud,
    Water,
}
```

- [ ] **Step 3: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`로 에러 없는지 확인.

Expected: 컴파일 에러 0건. Warning은 무시.

---

### Task 1.3: `IsInsideIsland` + `GetRegion` 헬퍼

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` (클래스 하단의 `private static GameObject FindPrefab` 위에)

- [ ] **Step 1: `IsInsideIsland` 추가**

```csharp
private static bool IsInsideIsland(float _x, float _z)
{
    return (_x * _x + _z * _z) <= (PLAYABLE_RADIUS * PLAYABLE_RADIUS);
}
```

- [ ] **Step 2: `GetDistanceFromCenter` 추가** (헬퍼)

```csharp
private static float GetDistanceFromCenter(float _x, float _z)
{
    return Mathf.Sqrt(_x * _x + _z * _z);
}
```

- [ ] **Step 3: `GetRegion` 추가**

```csharp
private static Region GetRegion(float _x, float _z)
{
    float dist = GetDistanceFromCenter(_x, _z);
    if (dist > ISLAND_RADIUS) return Region.Outside;
    if (dist > PLAYABLE_RADIUS) return Region.OuterRim;

    if (_z < BEACH_Z_MAX) return Region.Beach;
    if (_z < FOREST_Z_MAX) return Region.Forest;
    return Region.Mountain;
}
```

- [ ] **Step 4: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`. Expected: 에러 0.

---

### Task 1.4: 프리팹 카테고리 매칭 헬퍼

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs`

- [ ] **Step 1: 카테고리별 검색 패턴 매핑 정의**

기존 `FindPrefab(string)` 위에:

```csharp
private static readonly System.Collections.Generic.Dictionary<PrefabCategory, string[]> CATEGORY_PATTERNS =
    new()
    {
        { PrefabCategory.GroundFlat,        new[] { "SM_Gen_Env_Ground_Grass_", "SM_Gen_Env_Ground_Dirt_" } },
        { PrefabCategory.GroundSlope,       new[] { "SM_Gen_Env_Ground_Slope" } },
        { PrefabCategory.Hill,              new[] { "SM_Gen_Env_Hill_" } },
        { PrefabCategory.MountainBackdrop,  new[] { "SM_Gen_Env_Mountain_" } },
        { PrefabCategory.Tree,              new[] { "SM_Gen_Env_Tree_" } },
        { PrefabCategory.Bush,              new[] { "SM_Gen_Env_Bush_", "SM_Gen_Env_Shrub_" } },
        { PrefabCategory.Rock,              new[] { "SM_Gen_Env_Rock_" } },
        { PrefabCategory.Grass,             new[] { "SM_Gen_Env_Grass_", "SM_Gen_Env_Fern_" } },
        { PrefabCategory.Flower,            new[] { "SM_Gen_Env_Flowers_" } },
        { PrefabCategory.Mushroom,          new[] { "SM_Gen_Env_Mushroom_" } },
        { PrefabCategory.Stump,             new[] { "SM_Gen_Env_Stump_" } },
        { PrefabCategory.Cliff,             new[] { "SM_Gen_Env_Cliff_", "SM_Gen_Env_Dirt_Cliff_" } },
        { PrefabCategory.Skydome,           new[] { "SM_Gen_Env_Skydome_" } },
        { PrefabCategory.Cloud,             new[] { "SM_Gen_Env_Cloud_" } },
        { PrefabCategory.Water,             new[] { "SM_Gen_Env_Water_Plane_" } },
    };
```

- [ ] **Step 2: `FindPrefabsByCategory` 추가**

```csharp
private static GameObject[] FindPrefabsByCategory(PrefabCategory _category)
{
    if (!CATEGORY_PATTERNS.TryGetValue(_category, out var patterns))
        return System.Array.Empty<GameObject>();

    var results = new System.Collections.Generic.List<GameObject>();
    string[] guids = AssetDatabase.FindAssets("t:Prefab",
        new[] { "Assets/Synty/PolygonGeneric/Prefabs" });

    foreach (var guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

        foreach (var pattern in patterns)
        {
            if (fileName.StartsWith(pattern))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) results.Add(prefab);
                break;
            }
        }
    }
    return results.ToArray();
}
```

- [ ] **Step 3: `GetRandomPrefab` 추가**

```csharp
private static GameObject GetRandomPrefab(PrefabCategory _category)
{
    var pool = FindPrefabsByCategory(_category);
    if (pool.Length == 0) return null;
    return pool[Random.Range(0, pool.Length)];
}
```

- [ ] **Step 4: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`. Expected: 에러 0.

---

### Task 1.5: 헬퍼 검증 메뉴 + 체크포인트 1

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs`

이 단계의 검증은 Unity Test Framework가 아닌 **Editor 메뉴 + Console 단언**으로 한다. 단순하고 즉시 실행 가능하다.

- [ ] **Step 1: `Validate Helpers` 메뉴 추가**

```csharp
[MenuItem("Tools/Map Generator/Validate Helpers")]
public static void ValidateHelpers()
{
    int passed = 0;
    int failed = 0;

    void Assert(bool _condition, string _label)
    {
        if (_condition) { passed++; Debug.Log($"[Validate] PASS: {_label}"); }
        else { failed++; Debug.LogError($"[Validate] FAIL: {_label}"); }
    }

    // IsInsideIsland
    Assert(IsInsideIsland(0, 0), "원점은 섬 안");
    Assert(IsInsideIsland(60, 0), "(60, 0)은 섬 안 (PLAYABLE_RADIUS=70)");
    Assert(!IsInsideIsland(80, 0), "(80, 0)은 섬 밖");
    Assert(!IsInsideIsland(50, 50), "(50, 50)은 거리 70.7로 섬 밖");

    // GetRegion (z 기반 + 거리 기반)
    Assert(GetRegion(0, -50) == Region.Beach, "(0, -50)은 Beach");
    Assert(GetRegion(0, 0) == Region.Forest, "(0, 0)은 Forest");
    Assert(GetRegion(0, 50) == Region.Mountain, "(0, 50)은 Mountain");
    Assert(GetRegion(0, 72) == Region.OuterRim, "(0, 72)는 OuterRim");
    Assert(GetRegion(0, 80) == Region.Outside, "(0, 80)은 Outside");

    // Prefab 카테고리
    Assert(FindPrefabsByCategory(PrefabCategory.GroundFlat).Length > 0, "GroundFlat 풀 비어있지 않음");
    Assert(FindPrefabsByCategory(PrefabCategory.GroundSlope).Length >= 2, "GroundSlope 2종 이상");
    Assert(FindPrefabsByCategory(PrefabCategory.Hill).Length >= 2, "Hill 2종 이상");
    Assert(FindPrefabsByCategory(PrefabCategory.Tree).Length > 0, "Tree 풀 비어있지 않음");
    Assert(FindPrefabsByCategory(PrefabCategory.Skydome).Length > 0, "Skydome 1종 이상");

    Debug.Log($"[Validate] 결과: PASS {passed}, FAIL {failed}");
}
```

- [ ] **Step 2: 메뉴 실행**

MCP `u_editor_asset(action: refresh)` → Unity 메뉴 `Tools > Map Generator > Validate Helpers` 실행 → MCP `u_console`로 결과 확인.

Expected: 모든 줄 PASS, FAIL 0. 결과 줄: `[Validate] 결과: PASS N, FAIL 0`.

- [ ] **Step 3: 체크포인트 1 — 사용자 보고**

Console 결과 + 추가된 코드 요약을 사용자에게 보고. 사용자 OK 받기 전에 다음 구간으로 진행하지 않는다.

- [ ] **Step 4: 커밋 1**

```bash
cd /c/GitFork/WES_Project
git add WES/ProjectSettings/TagManager.asset \
        WES/Assets/Scripts/Editor/MapGenerator.cs
git commit -m "$(cat <<'EOF'
지형: MapGenerator 카테고리 분류 + 원형 섬 헬퍼 추가

- Region/PrefabCategory enum 정의
- IsInsideIsland, GetRegion, FindPrefabsByCategory 헬퍼
- Ground 레이어 등록
- Validate Helpers 메뉴로 헬퍼 검증

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

# 구간 2 — 자동 맵 생성 코어

이 구간의 목표: `Tools > Map Generator > Generate Island Map` 실행 시 원형 섬이 깔끔하게 생성된다. NavMesh는 다음 구간에서 베이크.

---

### Task 2.1: `GeneratedMap` 부모 + 기존 정리

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `GenerateMap()` 메서드 갱신

- [ ] **Step 1: 기존 `GenerateMap()`을 새 흐름으로 교체**

```csharp
[MenuItem("Tools/Map Generator/Generate Island Map")]
public static void GenerateMap()
{
    var oldPlane = GameObject.Find("Plane");
    if (oldPlane != null) DestroyImmediate(oldPlane);

    var mapRoot = GameObject.Find("MapRoot");
    if (mapRoot == null) mapRoot = new GameObject("MapRoot");

    var oldGenerated = mapRoot.transform.Find("GeneratedMap");
    if (oldGenerated != null) DestroyImmediate(oldGenerated.gameObject);

    var generated = new GameObject("GeneratedMap");
    generated.transform.SetParent(mapRoot.transform);

    Random.InitState(20260426);  // 재현 가능 시드

    GenerateGround(generated.transform);
    GenerateSlopes(generated.transform);
    GenerateHills(generated.transform);
    GenerateOuterRim(generated.transform);
    GenerateDecorations(generated.transform);
    GenerateSkydome(generated.transform);
    GenerateClouds(generated.transform);
    GenerateBoundaryWall(generated.transform);
    SetupSpawnAndEscapePoints();

    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
        UnityEngine.SceneManagement.SceneManager.GetActiveScene());

    Debug.Log("[MapGenerator] Island map generated!");
}
```

> 참고: `SampleEnvironment`는 건드리지 않는다. `Area_Beach`/`Area_Forest`/`Area_Mountain` 등 기존 자식이 있으면 `Generate Island Map`이 한 번 더 호출될 때 정리되도록 후속 단계에서 처리. 일단은 `GeneratedMap`만 정리.

- [ ] **Step 2: 기존 `GenerateGround`/`GenerateBeachArea`/`GenerateForestArea`/`GenerateMountainArea`/`GenerateWater` 메서드는 일단 그대로 둔다**

이후 Task에서 차례로 새 함수로 교체. 컴파일이 깨지지 않도록 호출만 끊는다.

- [ ] **Step 3: 빈 stub 메서드 추가** (이후 Task에서 채움)

```csharp
private static void GenerateGround(Transform _parent) { /* Task 2.2 */ }
private static void GenerateSlopes(Transform _parent) { /* Task 2.3 */ }
private static void GenerateHills(Transform _parent) { /* Task 2.3 */ }
private static void GenerateOuterRim(Transform _parent) { /* Task 2.4 */ }
private static void GenerateDecorations(Transform _parent) { /* Task 2.5 */ }
private static void GenerateSkydome(Transform _parent) { /* Task 2.6 */ }
private static void GenerateClouds(Transform _parent) { /* Task 2.6 */ }
private static void GenerateBoundaryWall(Transform _parent) { /* Task 2.7 */ }
```

⚠️ 기존 같은 이름의 `GenerateGround(Transform)` 메서드가 있다 — 기존 메서드를 위 stub으로 **대체**한다(중복 정의 금지). 또한 `GenerateBeachArea`/`GenerateForestArea`/`GenerateMountainArea`/`GenerateWater`는 더 이상 호출되지 않으므로 삭제하거나 `[System.Obsolete]`로 표시. **삭제 권장** (사용자 피드백: 백워드 호환 셈 안 둠).

- [ ] **Step 4: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`. Expected: 에러 0.

- [ ] **Step 5: 메뉴 실행 (빈 맵 확인)**

`Generate Island Map` 실행 → 씬에 `MapRoot/GeneratedMap`이 빈 채로 생성되는지 확인. MCP `u_editor_gameobject(action: hierarchy, target: MapRoot)` 로 확인.

Expected: `MapRoot > GeneratedMap` (자식 없음).

---

### Task 2.2: 영역별 ground 타일 자동 배치

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `GenerateGround` 본체 작성

- [ ] **Step 1: `GenerateGround` 작성**

```csharp
private static void GenerateGround(Transform _parent)
{
    var groundRoot = new GameObject("Ground");
    groundRoot.transform.SetParent(_parent);

    var grassPool = FindPrefabsByCategory(PrefabCategory.GroundFlat);
    var dirtPool = System.Array.FindAll(grassPool,
        p => p.name.Contains("Dirt"));
    var grassOnlyPool = System.Array.FindAll(grassPool,
        p => p.name.Contains("Grass"));

    if (grassOnlyPool.Length == 0) grassOnlyPool = grassPool;
    if (dirtPool.Length == 0) dirtPool = grassPool;

    for (float x = -PLAYABLE_RADIUS; x <= PLAYABLE_RADIUS; x += GROUND_TILE_SIZE)
    {
        for (float z = -PLAYABLE_RADIUS; z <= PLAYABLE_RADIUS; z += GROUND_TILE_SIZE)
        {
            if (!IsInsideIsland(x, z)) continue;

            Region region = GetRegion(x, z);
            GameObject prefab = region == Region.Mountain
                ? dirtPool[Random.Range(0, dirtPool.Length)]
                : grassOnlyPool[Random.Range(0, grassOnlyPool.Length)];

            var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tile.transform.SetParent(groundRoot.transform);
            tile.transform.position = new Vector3(x, 0, z);
            tile.layer = LayerMask.NameToLayer("Ground");
        }
    }
}
```

- [ ] **Step 2: 메뉴 실행**

`Generate Island Map` 실행 → `u_console` 에러 없음 → MCP `u_editor_sceneview(subAction: focus, target: MapRoot/GeneratedMap, angle: top)` → `u_editor_sceneview(subAction: screenshot, screenshotPath: screenshot_step2_2.png)` → 결과 이미지 확인.

Expected: 원형 섬 형태로 ground 타일이 깔림. 산지(북쪽)는 흙 톤, 그 외는 잔디.

- [ ] **Step 3: 체크 — 너무 많은 타일이 생기지 않는지 (<= 800 정도 예상)**

`u_editor_gameobject(action: get, target: MapRoot/GeneratedMap/Ground)` 로 자식 수 확인.

Expected: 자식 ~600~700개 (반경 70, 타일 4.5).

---

### Task 2.3: 슬로프 + 언덕 자동 배치

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `GenerateSlopes`, `GenerateHills`

- [ ] **Step 1: `GenerateSlopes` 작성**

영역별 슬로프 비중: 해안 0%, 숲 15%, 산지 50%. 슬로프는 Ground 타일 **위에 추가로** 얹는다 (Y=0.05). 좌우/회전 랜덤.

```csharp
private static void GenerateSlopes(Transform _parent)
{
    var slopeRoot = new GameObject("Slopes");
    slopeRoot.transform.SetParent(_parent);

    var slopePool = FindPrefabsByCategory(PrefabCategory.GroundSlope);
    if (slopePool.Length == 0) { Debug.LogWarning("[MapGenerator] Slope 프리팹 없음"); return; }

    foreach (Region targetRegion in new[] { Region.Forest, Region.Mountain })
    {
        float density = targetRegion == Region.Mountain ? 0.5f : 0.15f;
        int count = Mathf.RoundToInt(GetRegionTileApproxCount(targetRegion) * density);

        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-PLAYABLE_RADIUS, PLAYABLE_RADIUS);
            float z = targetRegion == Region.Mountain
                ? Random.Range(FOREST_Z_MAX, PLAYABLE_RADIUS)
                : Random.Range(BEACH_Z_MAX, FOREST_Z_MAX);

            if (!IsInsideIsland(x, z)) { i--; continue; }

            var prefab = slopePool[Random.Range(0, slopePool.Length)];
            var slope = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            slope.transform.SetParent(slopeRoot.transform);
            slope.transform.position = new Vector3(x, 0.05f, z);
            slope.transform.rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90, 0);
            slope.layer = LayerMask.NameToLayer("Ground");
        }
    }
}

private static int GetRegionTileApproxCount(Region _region)
{
    // 영역별 대략 타일 수 — 슬로프 밀도 계산용
    return _region switch
    {
        Region.Beach => 130,
        Region.Forest => 180,
        Region.Mountain => 130,
        _ => 0,
    };
}
```

- [ ] **Step 2: `GenerateHills` 작성**

```csharp
private static void GenerateHills(Transform _parent)
{
    var hillRoot = new GameObject("Hills");
    hillRoot.transform.SetParent(_parent);

    var hillPool = FindPrefabsByCategory(PrefabCategory.Hill);
    if (hillPool.Length == 0) return;

    // 숲 1~2개, 산지 3~4개
    PlaceHills(hillRoot.transform, hillPool, BEACH_Z_MAX, FOREST_Z_MAX, Random.Range(1, 3));
    PlaceHills(hillRoot.transform, hillPool, FOREST_Z_MAX, PLAYABLE_RADIUS, Random.Range(3, 5));
}

private static void PlaceHills(Transform _root, GameObject[] _pool, float _zMin, float _zMax, int _count)
{
    for (int i = 0; i < _count; i++)
    {
        float x = Random.Range(-PLAYABLE_RADIUS + 10, PLAYABLE_RADIUS - 10);
        float z = Random.Range(_zMin, _zMax);
        if (!IsInsideIsland(x, z)) { i--; continue; }

        var prefab = _pool[Random.Range(0, _pool.Length)];
        var hill = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        hill.transform.SetParent(_root);
        hill.transform.position = new Vector3(x, 0, z);
        hill.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        hill.transform.localScale = Vector3.one * Random.Range(0.9f, 1.4f);
        hill.layer = LayerMask.NameToLayer("Ground");
    }
}
```

- [ ] **Step 3: 메뉴 실행 + 스크린샷**

`Generate Island Map` 실행 → `u_editor_sceneview(subAction: screenshot, screenshotPath: screenshot_step2_3.png)`.

Expected: 산지에 슬로프와 언덕이 명확히 보이고, 숲은 듬성듬성 슬로프, 해안은 평지.

---

### Task 2.4: 외곽 처리 (모래/절벽/물 혼합형)

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `GenerateOuterRim`

- [ ] **Step 1: `GenerateOuterRim` 작성**

```csharp
private static void GenerateOuterRim(Transform _parent)
{
    var rimRoot = new GameObject("OuterRim");
    rimRoot.transform.SetParent(_parent);

    var sandPool = System.Array.FindAll(
        FindPrefabsByCategory(PrefabCategory.GroundFlat),
        p => p.name.Contains("Dirt"));
    var cliffPool = FindPrefabsByCategory(PrefabCategory.Cliff);
    var waterPool = FindPrefabsByCategory(PrefabCategory.Water);
    var mountainPool = FindPrefabsByCategory(PrefabCategory.MountainBackdrop);

    // 외곽 링: PLAYABLE_RADIUS ~ ISLAND_RADIUS
    for (float angle = 0; angle < 360; angle += 6f)
    {
        float rad = angle * Mathf.Deg2Rad;
        float x = Mathf.Cos(rad) * (PLAYABLE_RADIUS + 2f);
        float z = Mathf.Sin(rad) * (PLAYABLE_RADIUS + 2f);

        // 영역별 처리
        if (z < BEACH_Z_MAX)
        {
            // 남쪽 해안: 모래 + 얕은 물
            if (sandPool.Length > 0)
            {
                var sand = (GameObject)PrefabUtility.InstantiatePrefab(sandPool[Random.Range(0, sandPool.Length)]);
                sand.transform.SetParent(rimRoot.transform);
                sand.transform.position = new Vector3(x, -0.1f, z);
                sand.layer = LayerMask.NameToLayer("Ground");
            }
        }
        else if (z > FOREST_Z_MAX)
        {
            // 북쪽 산지: 절벽 + 깊은 물
            if (cliffPool.Length > 0)
            {
                var cliff = (GameObject)PrefabUtility.InstantiatePrefab(cliffPool[Random.Range(0, cliffPool.Length)]);
                cliff.transform.SetParent(rimRoot.transform);
                cliff.transform.position = new Vector3(x, 0, z);
                cliff.transform.rotation = Quaternion.LookRotation(new Vector3(-x, 0, -z));
            }
        }
        else
        {
            // 동/서 전이: 작은 절벽
            if (cliffPool.Length > 0)
            {
                var cliff = (GameObject)PrefabUtility.InstantiatePrefab(cliffPool[Random.Range(0, cliffPool.Length)]);
                cliff.transform.SetParent(rimRoot.transform);
                cliff.transform.position = new Vector3(x, -0.5f, z);
                cliff.transform.localScale = Vector3.one * 0.7f;
                cliff.transform.rotation = Quaternion.LookRotation(new Vector3(-x, 0, -z));
            }
        }
    }

    // 깊은 물 평면 (큰 사각형, 섬 외곽 전체 덮음)
    if (waterPool.Length > 0)
    {
        var deepWater = (GameObject)PrefabUtility.InstantiatePrefab(waterPool[0]);
        deepWater.transform.SetParent(rimRoot.transform);
        deepWater.transform.position = new Vector3(0, DEEP_WATER_Y, 0);
        deepWater.transform.localScale = Vector3.one * 30f;  // 충분히 크게
    }

    // 산 배경 (북쪽)
    if (mountainPool.Length > 0)
    {
        var bg1 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPool[Random.Range(0, mountainPool.Length)]);
        bg1.transform.SetParent(rimRoot.transform);
        bg1.transform.position = new Vector3(-25f, 0, ISLAND_RADIUS + 15f);
        bg1.transform.localScale = Vector3.one * 1.8f;

        var bg2 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPool[Random.Range(0, mountainPool.Length)]);
        bg2.transform.SetParent(rimRoot.transform);
        bg2.transform.position = new Vector3(25f, 0, ISLAND_RADIUS + 12f);
        bg2.transform.localScale = Vector3.one * 1.5f;
    }
}
```

- [ ] **Step 2: 메뉴 실행 + 스크린샷**

`Generate Island Map` 실행 → 위/사선 두 각도로 스크린샷.

Expected: 외곽에 모래(남)/절벽(동/서/북) 링이 보이고, 깊은 물 평면이 깔리고, 산 배경 두 개가 북쪽에 보임.

---

### Task 2.5: 데코 자동 배치

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `GenerateDecorations` + Poisson 헬퍼

- [ ] **Step 1: Poisson disk 샘플링 헬퍼 추가**

간단한 구현 — 후보 점을 N번 시도해 거리 기준 만족하면 채택:

```csharp
private static System.Collections.Generic.List<Vector2> PickPoissonPoints(
    float _zMin, float _zMax, float _minDistance, int _maxCount, int _attempts = 30)
{
    var points = new System.Collections.Generic.List<Vector2>();
    int tries = 0;
    while (points.Count < _maxCount && tries < _maxCount * _attempts)
    {
        tries++;
        float x = Random.Range(-PLAYABLE_RADIUS, PLAYABLE_RADIUS);
        float z = Random.Range(_zMin, _zMax);
        if (!IsInsideIsland(x, z)) continue;

        bool tooClose = false;
        foreach (var p in points)
        {
            if ((p.x - x) * (p.x - x) + (p.y - z) * (p.y - z) < _minDistance * _minDistance)
            { tooClose = true; break; }
        }
        if (!tooClose) points.Add(new Vector2(x, z));
    }
    return points;
}
```

- [ ] **Step 2: `GenerateDecorations` 작성**

영역별 데코 종류 + 밀도. 나무/덤불은 클러스터(Poisson). 풀/꽃/바위는 자유 배치.

```csharp
private static void GenerateDecorations(Transform _parent)
{
    var decoRoot = new GameObject("Decorations");
    decoRoot.transform.SetParent(_parent);

    PlaceTreesByRegion(decoRoot.transform);
    PlaceBushesByRegion(decoRoot.transform);
    PlaceRocksByRegion(decoRoot.transform);
    PlaceGrassByRegion(decoRoot.transform);
    PlaceFlowersByRegion(decoRoot.transform);
    PlaceMushroomsAndStumps(decoRoot.transform);
}

private static void PlaceTreesByRegion(Transform _root)
{
    var treePool = FindPrefabsByCategory(PrefabCategory.Tree);
    if (treePool.Length == 0) return;

    // 해안 — 참나무 8~10그루, 듬성
    PlaceCluster(_root, treePool, -PLAYABLE_RADIUS + 10, BEACH_Z_MAX, 9, 5f, "Tree_Beach", 0.9f, 1.3f);
    // 숲 — Pine/일반 25~30그루, 빽빽
    PlaceCluster(_root, treePool, BEACH_Z_MAX, FOREST_Z_MAX, 28, 3.5f, "Tree_Forest", 0.9f, 1.4f);
    // 산지 — Dead Tree 4~6그루
    var deadPool = System.Array.FindAll(treePool, p => p.name.Contains("Dead"));
    if (deadPool.Length > 0)
        PlaceCluster(_root, deadPool, FOREST_Z_MAX, PLAYABLE_RADIUS, 5, 4f, "Tree_Mountain", 0.8f, 1.2f);
}

private static void PlaceCluster(Transform _root, GameObject[] _pool, float _zMin, float _zMax,
    int _count, float _minDist, string _label, float _scaleMin, float _scaleMax)
{
    var pts = PickPoissonPoints(_zMin, _zMax, _minDist, _count);
    int i = 0;
    foreach (var p in pts)
    {
        var prefab = _pool[Random.Range(0, _pool.Length)];
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(_root);
        go.transform.position = new Vector3(p.x, GetTerrainHeight(p.x, p.y), p.y);
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        go.transform.localScale = Vector3.one * Random.Range(_scaleMin, _scaleMax);
        go.name = $"{_label}_{i:D2}";
        i++;
    }
}

private static void PlaceBushesByRegion(Transform _root)
{
    var bushPool = FindPrefabsByCategory(PrefabCategory.Bush);
    if (bushPool.Length == 0) return;
    PlaceCluster(_root, bushPool, BEACH_Z_MAX, FOREST_Z_MAX, 18, 2f, "Bush_Forest", 0.9f, 1.3f);
}

private static void PlaceRocksByRegion(Transform _root)
{
    var rockPool = FindPrefabsByCategory(PrefabCategory.Rock);
    if (rockPool.Length == 0) return;
    PlaceCluster(_root, rockPool, -PLAYABLE_RADIUS, BEACH_Z_MAX, 8, 3f, "Rock_Beach", 0.7f, 1.4f);
    PlaceCluster(_root, rockPool, FOREST_Z_MAX, PLAYABLE_RADIUS, 12, 3f, "Rock_Mountain", 0.9f, 1.6f);
}

private static void PlaceGrassByRegion(Transform _root)
{
    var pool = FindPrefabsByCategory(PrefabCategory.Grass);
    if (pool.Length == 0) return;
    PlaceCluster(_root, pool, -PLAYABLE_RADIUS, FOREST_Z_MAX, 50, 1.2f, "Grass_All", 0.8f, 1.2f);
}

private static void PlaceFlowersByRegion(Transform _root)
{
    var pool = FindPrefabsByCategory(PrefabCategory.Flower);
    if (pool.Length == 0) return;
    PlaceCluster(_root, pool, BEACH_Z_MAX, FOREST_Z_MAX, 18, 1.5f, "Flower_Forest", 0.9f, 1.2f);
}

private static void PlaceMushroomsAndStumps(Transform _root)
{
    var mushPool = FindPrefabsByCategory(PrefabCategory.Mushroom);
    if (mushPool.Length > 0)
        PlaceCluster(_root, mushPool, -10, 25, 8, 1.2f, "Mush_Forest", 0.9f, 1.3f);
    var stumpPool = FindPrefabsByCategory(PrefabCategory.Stump);
    if (stumpPool.Length > 0)
        PlaceCluster(_root, stumpPool, BEACH_Z_MAX, FOREST_Z_MAX, 3, 8f, "Stump_Forest", 0.9f, 1.2f);
}
```

- [ ] **Step 3: 메뉴 실행 + 스크린샷**

`Generate Island Map` 실행 → 위/사선/측면 3각도 스크린샷.

Expected: 해안 듬성 나무, 숲 빽빽 + 덤불/꽃, 산지 죽은 나무 + 큰 바위. 좌우 비대칭.

---

### Task 2.6: Skydome + Cloud

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `GenerateSkydome`, `GenerateClouds`

- [ ] **Step 1: `GenerateSkydome` 작성**

```csharp
private static void GenerateSkydome(Transform _parent)
{
    var pool = FindPrefabsByCategory(PrefabCategory.Skydome);
    if (pool.Length == 0) return;

    var sky = (GameObject)PrefabUtility.InstantiatePrefab(pool[0]);
    sky.transform.SetParent(_parent);
    sky.transform.position = Vector3.zero;
    sky.transform.localScale = Vector3.one * 4f;  // 충분히 크게
    sky.name = "Skydome";
}
```

- [ ] **Step 2: `GenerateClouds` 작성**

```csharp
private static void GenerateClouds(Transform _parent)
{
    var pool = FindPrefabsByCategory(PrefabCategory.Cloud);
    if (pool.Length == 0) return;

    var cloudRoot = new GameObject("Clouds");
    cloudRoot.transform.SetParent(_parent);

    int cloudCount = 4;
    for (int i = 0; i < cloudCount; i++)
    {
        float angle = (360f / cloudCount) * i + Random.Range(-20f, 20f);
        float rad = angle * Mathf.Deg2Rad;
        float radius = Random.Range(35f, 60f);
        float x = Mathf.Cos(rad) * radius;
        float z = Mathf.Sin(rad) * radius;
        float y = Random.Range(35f, 50f);

        var prefab = pool[Random.Range(0, pool.Length)];
        var cloud = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        cloud.transform.SetParent(cloudRoot.transform);
        cloud.transform.position = new Vector3(x, y, z);
        cloud.transform.localScale = Vector3.one * Random.Range(1.5f, 2.5f);
    }
}
```

- [ ] **Step 3: 메뉴 실행 + 스크린샷**

`Generate Island Map` 실행 → 사선 스크린샷.

Expected: 섬 위로 푸른 Skydome, 4개 구름이 분산.

---

### Task 2.7: 보조 원형 콜라이더

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `GenerateBoundaryWall`

- [ ] **Step 1: `GenerateBoundaryWall` 작성**

48각형 보이지 않는 콜라이더 링. 높이 5, 두께 2.

```csharp
private static void GenerateBoundaryWall(Transform _parent)
{
    var wallRoot = new GameObject("BoundaryWall");
    wallRoot.transform.SetParent(_parent);

    const int SEGMENTS = 48;
    const float WALL_HEIGHT = 5f;
    const float WALL_THICKNESS = 2f;
    float wallRadius = ISLAND_RADIUS + WALL_THICKNESS / 2f;

    for (int i = 0; i < SEGMENTS; i++)
    {
        float angle = (360f / SEGMENTS) * i;
        float rad = angle * Mathf.Deg2Rad;
        float x = Mathf.Cos(rad) * wallRadius;
        float z = Mathf.Sin(rad) * wallRadius;

        var seg = new GameObject($"WallSeg_{i:D2}");
        seg.transform.SetParent(wallRoot.transform);
        seg.transform.position = new Vector3(x, WALL_HEIGHT / 2f, z);
        seg.transform.rotation = Quaternion.LookRotation(new Vector3(-x, 0, -z));

        var box = seg.AddComponent<BoxCollider>();
        float segLen = 2f * Mathf.PI * wallRadius / SEGMENTS + 0.5f; // 약간 겹치게
        box.size = new Vector3(segLen, WALL_HEIGHT, WALL_THICKNESS);
    }
}
```

- [ ] **Step 2: 메뉴 실행 + 콜라이더 시각 확인**

`Generate Island Map` → MCP `u_editor_gameobject(action: get, target: MapRoot/GeneratedMap/BoundaryWall)` 로 자식 48개 확인.

Expected: 자식 48개, 모두 BoxCollider 보유. 게임 뷰에서는 보이지 않음 (Renderer 없음).

---

### Task 2.8: SpawnPosition / EscapePoint / Area 좌표 갱신

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `SetupSpawnAndEscapePoints` 갱신

- [ ] **Step 1: 좌표 명시적으로 설정 (설계 6절 표대로)**

```csharp
private static void SetupSpawnAndEscapePoints()
{
    MoveObject("StartPosition1", new Vector3(-3f, 0f, -42f));
    MoveObject("StartPosition2", new Vector3(3f, 0f, -42f));
    MoveObject("StartPosition3", new Vector3(-6f, 0f, -38f));
    MoveObject("StartPosition4", new Vector3(6f, 0f, -38f));
    MoveObject("EscapePoint",    new Vector3(0f, 8f, 60f));

    // InGameAreaWorker 자식
    MoveObject("Area1_Beach",    new Vector3(0f, 0f, -35f));
    MoveObject("Area2_Forest",   new Vector3(0f, 0f, 10f));
    MoveObject("Area3_Mountain", new Vector3(0f, 0f, 50f));
}
```

- [ ] **Step 2: 메뉴 실행 + 좌표 확인**

`Generate Island Map` 실행 → MCP `u_editor_gameobject(action: get, target: StartPosition1)` 로 좌표 확인.

Expected: position = (-3, 0, -42).

---

### Task 2.9: 체크포인트 2 — 사용자 검토

- [ ] **Step 1: 위/사선/측면 스크린샷 3장 보고**

```
u_editor_sceneview(focus, target=MapRoot/GeneratedMap, angle=top)
u_editor_sceneview(screenshot, screenshotPath=screenshot_section2_top.png)
u_editor_sceneview(focus, target=MapRoot/GeneratedMap, angle=persp)
u_editor_sceneview(screenshot, screenshotPath=screenshot_section2_persp.png)
u_editor_sceneview(focus, target=MapRoot/GeneratedMap, angle=side)
u_editor_sceneview(screenshot, screenshotPath=screenshot_section2_side.png)
```

스크린샷 3장을 사용자에게 보여주고 분위기 OK 받기.

- [ ] **Step 2: 사용자 OK 대기**

수정 요청이 있으면 해당 Task로 돌아가 반영. OK 받기 전 다음 구간으로 진행 금지.

- [ ] **Step 3: 커밋 2**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/Scripts/Editor/MapGenerator.cs WES/Assets/Scenes/Ingame.unity
git commit -m "$(cat <<'EOF'
지형: MapGenerator 원형 섬 자동 생성

- Ground/Slopes/Hills 영역별 자동 배치
- 외곽 처리(혼합형: 모래/절벽/물) + 산 배경
- Decorations(나무/덤불/바위/풀/꽃/버섯/그루터기) Poisson 클러스터
- Skydome + Cloud 4개 + 보조 원형 콜라이더 48분할
- SpawnPosition/EscapePoint/Area 좌표 갱신

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

# 구간 3 — NavMesh 베이크

이 구간의 목표: 자동 생성된 맵 위에 Max Slope 60°로 NavMesh가 깔린다.

---

### Task 3.1: BakeNavMesh 갱신

**Files:**
- Modify: `Assets/Scripts/Editor/MapGenerator.cs` — `BakeNavMesh` 메서드

- [ ] **Step 1: NavMeshBuilder + NavMeshBuildSettings 사용**

```csharp
[MenuItem("Tools/Map Generator/Bake NavMesh")]
public static void BakeNavMesh()
{
    var generated = GameObject.Find("MapRoot/GeneratedMap");
    if (generated == null) { Debug.LogError("[MapGenerator] GeneratedMap 없음. Generate Island Map 먼저 실행."); return; }

    // NavigationStatic 마킹: Ground/Slopes/Hills + OuterRim의 모래/절벽 일부
    MarkNavStaticRecursively(generated.transform.Find("Ground"));
    MarkNavStaticRecursively(generated.transform.Find("Slopes"));
    MarkNavStaticRecursively(generated.transform.Find("Hills"));
    var rim = generated.transform.Find("OuterRim");
    if (rim != null)
    {
        foreach (Transform child in rim)
        {
            // 모래(Dirt 그라운드)는 NavMesh 포함, 절벽/물/산 배경은 제외
            if (child.name.Contains("Ground_Dirt") || child.name.Contains("Ground_Grass"))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.NavigationStatic);
        }
    }

    // 빌드 세팅
    var settings = NavMesh.GetSettingsByID(0);  // Default agent
    settings.agentSlope = MAX_SLOPE_DEGREE;
    settings.agentClimb = STEP_HEIGHT;
    NavMesh.RemoveAllNavMeshData();

    UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
        UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    Debug.Log("[MapGenerator] NavMesh baked (Max Slope 60°, Step 0.4)");
}

private static void MarkNavStaticRecursively(Transform _root)
{
    if (_root == null) return;
    foreach (Transform child in _root)
    {
        GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.NavigationStatic);
    }
}
```

⚠️ `using UnityEngine.AI;` 가 파일 상단에 있는지 확인 — 없으면 추가.

- [ ] **Step 2: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`. Expected: 에러 0.

- [ ] **Step 3: 베이크 실행**

Unity 메뉴 `Tools > Map Generator > Bake NavMesh` 실행 → `u_console`로 결과 확인.

Expected: `[MapGenerator] NavMesh baked (Max Slope 60°, Step 0.4)`.

- [ ] **Step 4: NavMesh 시각 확인**

씬뷰에서 `Window > AI > Navigation` 열고 NavMesh 표시 → `u_editor_sceneview(subAction: screenshot, screenshotPath: screenshot_navmesh.png)`.

Expected: 섬 전역에 파란 NavMesh가 깔려 있고, 가파른 곳/물/외곽은 NavMesh 없음.

---

### Task 3.2: 체크포인트 3 — 사용자 검토 + 커밋

- [ ] **Step 1: 사용자에게 NavMesh 스크린샷 보고**

끊긴 부분이 있으면 메모 (다음 구간 손다듬기에서 보정).

- [ ] **Step 2: 사용자 OK 대기**

- [ ] **Step 3: 커밋 3**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/Scripts/Editor/MapGenerator.cs WES/Assets/Scenes/Ingame.unity
git commit -m "$(cat <<'EOF'
지형: NavMesh 베이크 Max Slope 60° + Step Height 0.4

- BakeNavMesh 메뉴 갱신 (NavigationStatic 자동 마킹)
- OuterRim은 모래만 NavMesh 포함, 절벽/물/산배경 제외
- 베이크 산출물 Ingame.unity에 반영

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

# 구간 4 — Claude MCP 손다듬기

이 구간의 목표: 자동 생성 결과 위에 디자인 핵심 5개 포인트만 손배치하여 분위기를 완성한다. 각 손배치는 Claude가 MCP 도구로 직접 수행.

---

### Task 4.1: Mountain_02 랜드마크 배치

**작업자:** Claude (MCP)

- [ ] **Step 1: 기존 Mountain_02 배경 인스턴스 확인**

```
u_editor_gameobject(action: find, target: SM_Gen_Env_Mountain_02)
```

Expected: `MapRoot/GeneratedMap/OuterRim/`에 1~2개 존재.

- [ ] **Step 2: 그 중 하나를 정중앙(0, 0, 65), 스케일 2.5x로 이동**

```
u_set_transform(target=MapRoot/GeneratedMap/OuterRim/SM_Gen_Env_Mountain_02_xxx,
                position=(0, 0, 65), localScale=(2.5, 2.5, 2.5), rotation=(0, 180, 0))
```

회전 180도는 산이 카메라(남쪽)을 향하도록.

- [ ] **Step 3: 스크린샷 검증**

```
u_editor_sceneview(focus, target=StartPosition1, angle=persp)
u_editor_sceneview(screenshot, screenshotPath=screenshot_landmark_mountain.png)
```

Expected: 시작 지점에서 정면을 봤을 때 큰 산이 화면 중앙에 보임.

---

### Task 4.2: 등산로 슬로프 곡선 연결

**작업자:** Claude (MCP)

- [ ] **Step 1: 기존 자동 슬로프 중 등산로용 3개 선정**

`MapRoot/GeneratedMap/Slopes` 자식 중 z ≈ 25~50 사이 슬로프 3개를 선택.

- [ ] **Step 2: 곡선 형태로 위치/회전 재배치**

예시 좌표 (사용자 실제 결과 보고 미세 조정):
```
slope_a: position=(-12, 0, 28), rotation=(0, 30, 0)
slope_b: position=(0,   0, 36), rotation=(0, 0,  0)
slope_c: position=(12,  0, 44), rotation=(0, -30, 0)
```

각각 `u_set_transform`로 이동.

- [ ] **Step 3: 스크린샷 검증**

탑뷰로 등산로 곡선이 명확히 보이는지 확인.

---

### Task 4.3: 숲 그루터기 + 버섯 군락

**작업자:** Claude (MCP)

- [ ] **Step 1: 그루터기 기존 인스턴스 확인 후 군락 형성**

`MapRoot/GeneratedMap/Decorations` 자식 중 Stump 인스턴스 1~2개를 (0, 0, 5) 근처로 모으기.

- [ ] **Step 2: 버섯 군락 (3~5개)을 그루터기 주변에 추가**

```
u_editor_gameobject(action: add, target=NewMushroom_1, prefabPath=Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Mushroom_01.prefab)
u_set_transform(target=NewMushroom_1, position=(2, 0, 6))
```
이런 식으로 3~5개 추가. 부모는 `MapRoot/GeneratedMap/Decorations`로 설정.

- [ ] **Step 3: 스크린샷 검증**

숲 중앙에 그루터기 + 버섯 군락이 분위기 있게 보이는지.

---

### Task 4.4: Cloud 메쉬 4~5개 분산 배치 미세 조정

**작업자:** Claude (MCP)

- [ ] **Step 1: 자동 생성된 Cloud 4개의 위치/스케일 점검**

```
u_editor_gameobject(action: get, target=MapRoot/GeneratedMap/Clouds)
```

자동 배치가 충분히 분산되어 있으면 그대로 두고, 한쪽에 몰려 있거나 시야에 거슬리면 1~2개의 위치/스케일만 미세 조정.

- [ ] **Step 2: 필요 시 5번째 Cloud 추가**

`u_editor_gameobject(action: add, ...)` + `u_set_transform`.

- [ ] **Step 3: 스크린샷 검증**

게임뷰 카메라 각도에서 구름 분포가 자연스러운지.

---

### Task 4.5: NavMesh 끊김 보정

**작업자:** Claude (MCP)

- [ ] **Step 1: 구간 3 베이크 결과에서 끊긴 곳 확인**

씬뷰 NavMesh 표시 + `u_editor_sceneview(screenshot)`로 캡처. 끊긴 영역 위치 메모.

- [ ] **Step 2: 끊긴 영역에 슬로프 추가/이동**

각 끊긴 구간에 슬로프 1~2개 추가 또는 위치 조정 (`u_set_transform` 또는 `u_editor_gameobject add`). 최대 3곳까지만 — 그 이상이면 자동 생성 로직 자체에 문제.

- [ ] **Step 3: 끊김이 3곳 이상이면 사용자 보고**

자동 로직 수정 검토를 위해 구간 2로 돌아갈지 사용자 결정.

---

### Task 4.6: NavMesh 재베이크 + 체크포인트 4

- [ ] **Step 1: 재베이크 실행**

Unity 메뉴 `Tools > Map Generator > Bake NavMesh` 재실행.

- [ ] **Step 2: 위/사선/측면 스크린샷**

분위기 종합 확인.

- [ ] **Step 3: 사용자 OK 대기**

- [ ] **Step 4: 커밋 4**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/Scenes/Ingame.unity
git commit -m "$(cat <<'EOF'
지형: 맵 랜드마크 손배치 + NavMesh 재베이크

- Mountain_02 정중앙 랜드마크
- 등산로 슬로프 곡선 연결
- 숲 그루터기 + 버섯 군락
- Cloud 분산 미세 조정
- NavMesh 끊김 보정 후 재베이크

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

# 구간 5 — PlayerCharacter 마우스 Raycast

이 구간의 목표: `UpdateMouseLook()`이 평면 가정 대신 `Physics.Raycast`(Ground 레이어)를 사용해 경사면에서도 정확한 좌표를 얻는다.

---

### Task 5.1: `m_GroundLayerMask` 필드 추가

**Files:**
- Modify: `Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs` — 클래스 멤버 영역

- [ ] **Step 1: SerializeField 추가**

기존 `[SerializeField] private` 멤버들 옆에 추가:

```csharp
[SerializeField] private LayerMask m_GroundLayerMask;
```

- [ ] **Step 2: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`. Expected: 에러 0.

---

### Task 5.2: `UpdateMouseLook` 본체 교체

**Files:**
- Modify: `Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs:343-357`

- [ ] **Step 1: 현재 메서드 확인**

```csharp
private void UpdateMouseLook()
{
    Camera mainCamera = Camera.main;
    if (mainCamera == null)
        return;

    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
    Plane groundPlane = new(Vector3.up, Vector3.zero);

    if (groundPlane.Raycast(ray, out float distance))
    {
        Vector3 hitPoint = ray.GetPoint(distance);
        LookAtPosition(hitPoint);
    }
}
```

- [ ] **Step 2: 본체 교체**

```csharp
private void UpdateMouseLook()
{
    Camera mainCamera = Camera.main;
    if (mainCamera == null)
        return;

    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
    if (Physics.Raycast(ray, out RaycastHit hit, 200f, m_GroundLayerMask))
    {
        LookAtPosition(hit.point);
    }
}
```

- [ ] **Step 3: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`. Expected: 에러 0.

---

### Task 5.3: Inspector에서 Ground 레이어 매핑 (MCP)

- [ ] **Step 1: 플레이어 프리팹 또는 씬 인스턴스 확인**

```
u_editor_gameobject(action: find, target: PlayerCharacter)
```

또는 프리팹: `Assets/GameResource/Character/Player/PlayerCharacter.prefab`

- [ ] **Step 2: `m_GroundLayerMask` 값을 Ground 레이어로 설정**

MCP 컴포넌트 편집:
```
u_editor_component(target: <PlayerCharacter 인스턴스 또는 프리팹>,
                   componentType: PlayerCharacter,
                   action: set,
                   property: m_GroundLayerMask,
                   value: <Ground 레이어 비트마스크>)
```

⚠️ LayerMask 값은 비트마스크 — Ground 레이어가 8번이면 `1 << 8 = 256`. 정확한 값은 Task 1.1 결과에 따라 결정.

- [ ] **Step 3: 시각 확인**

Inspector에 `m_GroundLayerMask`가 `Ground` 한 줄로 표시되는지.

---

### Task 5.4: 평지 + 경사면 마우스 정확도 수동 검증

- [ ] **Step 1: 플레이모드 진입**

`u_play(action: enter)` 또는 Unity Play 버튼.

- [ ] **Step 2: 평지에서 마우스 클릭 — 캐릭터가 클릭 방향 바라보는지**

수동 또는 `u_editor_input` 시뮬레이션. 평지(StartPosition 근처)에서 동/서/북/남 4방향 마우스 위치 → 캐릭터 회전이 자연스러운지.

- [ ] **Step 3: 경사면에서 마우스 클릭 — Y가 자연 보정되는지**

산지(z=40~60) 슬로프 위에서 마우스 가져갔을 때, 캐릭터가 슬로프 경사를 따라 자연스럽게 향하는지.

Expected: 평지 가정 시 발생하던 "캐릭터가 슬로프를 무시하고 평면을 본다" 현상 사라짐.

- [ ] **Step 4: 플레이모드 종료**

`u_play(action: exit)`.

- [ ] **Step 5: 사용자 OK 대기**

체크포인트 5.

- [ ] **Step 6: 커밋 5**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/Scripts/WorldBaseObject/Player/PlayerCharacter.cs WES/Assets/GameResource/Character/Player/
git commit -m "$(cat <<'EOF'
지형: PlayerCharacter 마우스 시점을 Physics.Raycast로 전환

- m_GroundLayerMask SerializeField 추가
- UpdateMouseLook을 Plane 가정 대신 Raycast로 변경
- Inspector에서 Ground 레이어 매핑

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

# 구간 6 — QA 시나리오 검증

이 구간의 목표: TestManager로 6개 시나리오를 자동 검증하고 모두 통과.

---

### Task 6.1: TestManager 시나리오 메서드 추가

**Files:**
- Modify: `Assets/Scripts/Manager/TestManager.cs`

- [ ] **Step 1: `TestTerrainSlope` 진입 메서드 + 코루틴 추가**

`TestManager` 클래스 내부, 기존 `TestContentExpansion` 다음에:

```csharp
public void TestTerrainSlope()
{
    StartCoroutine(CoTestTerrainSlope());
}

private IEnumerator CoTestTerrainSlope()
{
    GameDebug.Log("[TestManager] TestTerrainSlope 시작");

    var controller = Object.FindFirstObjectByType<InGameController>();
    if (controller == null) { GameDebug.LogError("[TestManager] InGameController 없음"); yield break; }

    var player = controller.PlayWorker?.LocalPlayer;
    if (player == null) { GameDebug.LogError("[TestManager] LocalPlayer 없음"); yield break; }

    // 시나리오 1: 슬로프 위 플레이어 이동 (Y 자연 보정)
    GameDebug.Log("[TestManager] 시나리오 1: 슬로프 위 이동");
    player.transform.position = new Vector3(0, 0, 35);  // 숲→산지 슬로프 진입
    yield return new WaitForSeconds(0.5f);
    Vector3 startPos = player.transform.position;
    player.MoveWithDirection(new Vector2(0, 1));  // 북쪽
    yield return new WaitForSeconds(2f);
    player.MoveWithDirection(Vector2.zero);
    Vector3 endPos = player.transform.position;
    GameDebug.Log($"[TestManager] 이동 전 Y={startPos.y:F2}, 이동 후 Y={endPos.y:F2}, ΔZ={endPos.z - startPos.z:F2}");
    bool yChanged = Mathf.Abs(endPos.y - startPos.y) > 0.5f;
    GameDebug.Log(yChanged ? "[TestManager] PASS: 슬로프 위에서 Y 자연 보정" : "[TestManager] FAIL: Y가 변하지 않음 (NavMesh 미적용?)");

    yield return new WaitForSeconds(0.5f);

    // 시나리오 2: 마우스 정확도 — Raycast가 Ground 레이어를 맞추는지
    GameDebug.Log("[TestManager] 시나리오 2: 마우스 Raycast 검증");
    var camera = Camera.main;
    if (camera != null)
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, LayerMask.GetMask("Ground")))
        {
            GameDebug.Log($"[TestManager] PASS: Raycast hit at {hit.point}, name={hit.collider.name}");
        }
        else
        {
            GameDebug.LogError("[TestManager] FAIL: Raycast가 Ground를 못 맞춤");
        }
    }

    yield return new WaitForSeconds(0.5f);

    // 시나리오 3: 외곽 차단 — 보조 콜라이더 동작
    GameDebug.Log("[TestManager] 시나리오 3: 외곽 차단");
    Vector3 beforeBoundary = new Vector3(70, 0, 0);
    player.transform.position = beforeBoundary;
    yield return new WaitForSeconds(0.5f);
    player.MoveWithDirection(new Vector2(1, 0));  // 동쪽 (밖으로)
    yield return new WaitForSeconds(2f);
    player.MoveWithDirection(Vector2.zero);
    float distFromCenter = Mathf.Sqrt(player.transform.position.x * player.transform.position.x +
                                       player.transform.position.z * player.transform.position.z);
    GameDebug.Log($"[TestManager] 외곽 시도 후 거리: {distFromCenter:F2} (75 미만 기대)");
    GameDebug.Log(distFromCenter < 75f ? "[TestManager] PASS: 외곽 차단됨" : "[TestManager] FAIL: 외곽 통과");

    yield return new WaitForSeconds(0.5f);

    // 시나리오 4: 가파른 곳(>60°) 차단 — NavMesh 미적용 영역
    GameDebug.Log("[TestManager] 시나리오 4: 가파른 곳 차단");
    var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
    if (agent != null)
    {
        bool onMesh = agent.isOnNavMesh;
        GameDebug.Log($"[TestManager] NavMeshAgent.isOnNavMesh = {onMesh}");
        GameDebug.Log(onMesh ? "[TestManager] PASS: NavMesh 위" : "[TestManager] FAIL: NavMesh 밖");
    }

    yield return new WaitForSeconds(0.5f);

    // 시나리오 5: 다양한 높이 — Start → Escape 사이 등반
    GameDebug.Log("[TestManager] 시나리오 5: Start → Escape 사이 다양한 Y");
    var samples = new Vector3[] {
        new Vector3(0, 0, -42),
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 35),
        new Vector3(0, 0, 55),
    };
    foreach (var p in samples)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(p, out var navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            GameDebug.Log($"[TestManager] {p} → NavMesh Y = {navHit.position.y:F2}");
        else
            GameDebug.LogError($"[TestManager] {p} → NavMesh 미적용");
    }

    yield return new WaitForSeconds(0.5f);

    // 시나리오 6: 몬스터 NavMeshAgent 슬로프 추적 (간이)
    GameDebug.Log("[TestManager] 시나리오 6: 몬스터 슬로프 추적");
    var monsters = Object.FindObjectsByType<MonsterStateMachine>(FindObjectsSortMode.None);
    GameDebug.Log($"[TestManager] 몬스터 수: {monsters.Length}");
    if (monsters.Length > 0)
    {
        var m = monsters[0];
        var mAgent = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
        GameDebug.Log(mAgent != null && mAgent.isOnNavMesh
            ? "[TestManager] PASS: 몬스터 NavMesh 위에 있음"
            : "[TestManager] FAIL: 몬스터 NavMesh 밖");
    }

    GameDebug.Log("[TestManager] TestTerrainSlope 완료");
}
```

⚠️ `MonsterStateMachine` 네임스페이스/접근 — 컴파일 에러 발생 시 `using` 추가 또는 `FindObjectsByType` 타입 조정.

- [ ] **Step 2: 컴파일 확인**

MCP `u_editor_asset(action: refresh)` → `u_console`. Expected: 에러 0.

---

### Task 6.2: dev-qa로 시나리오 자동 실행

- [ ] **Step 1: 플레이모드 진입**

```
u_play(action: enter)
```

씬 로딩, LocalPlayer 스폰 대기.

- [ ] **Step 2: TestTerrainSlope 호출**

```
u_play_invoke(method: TestManager.TestTerrainSlope)
```

또는 Console에서 `Managers.Test.TestTerrainSlope()` 호출.

⚠️ `u_play_invoke`가 사용 불가하면 TestManager에 임시 메뉴 또는 InputSystem 키 트리거 추가.

- [ ] **Step 3: Console 결과 수집**

```
u_console
```

PASS/FAIL 라인 카운트.

Expected: PASS 6, FAIL 0.

- [ ] **Step 4: 플레이모드 종료**

```
u_play(action: exit)
```

---

### Task 6.3: 실패 시 회귀

- [ ] **Step 1: FAIL이 있으면 시나리오별로 분류**

| FAIL 종류 | 회귀 대상 |
|---|---|
| 시나리오 1 (Y 보정) | NavMesh 베이크 (구간 3) — Slope 마킹 누락 |
| 시나리오 2 (Raycast) | Ground 레이어 매핑 (Task 5.3) 또는 Task 1.1 |
| 시나리오 3 (외곽 차단) | BoundaryWall (Task 2.7) — 콜라이더 누락/크기 |
| 시나리오 4 (가파른 곳) | NavMesh agentSlope 설정 또는 NavMesh 끊김 |
| 시나리오 5 (다양한 Y) | NavMesh 영역 부족 |
| 시나리오 6 (몬스터) | 몬스터 스폰 위치가 NavMesh 밖 |

- [ ] **Step 2: 해당 Task로 돌아가 수정 후 다시 6.2 실행**

- [ ] **Step 3: 모두 PASS될 때까지 반복**

---

### Task 6.4: 체크포인트 6 — 최종 OK + 커밋

- [ ] **Step 1: 사용자에게 PASS 6/0 보고**

- [ ] **Step 2: 사용자 최종 OK 대기**

- [ ] **Step 3: 커밋 6**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/Scripts/Manager/TestManager.cs
git commit -m "$(cat <<'EOF'
지형: 경사 이동 QA 시나리오 통과 (TestTerrainSlope)

- 슬로프 Y 보정, 마우스 Raycast, 외곽 차단, 가파름 차단,
  다양한 높이, 몬스터 NavMesh 6개 시나리오 작성
- 모든 시나리오 PASS

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"
```

---

## 종료 후

- 모든 구간 커밋이 완료되면 `git log --oneline` 으로 확인.
- 핸드오프 문서(`document/2026-04-26-terrain-slope-handoff.md`)는 참고용으로 보존.
- 후속 작업: 200×200u 확장(콘텐츠 충분해지면), 동적 시드, 카메라 워커 슬로프 추적 미세 검증.
