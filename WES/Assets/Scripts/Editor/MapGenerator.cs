#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.AI;

/// <summary>
/// R2 6지역 선형 종단 회랑 맵 자동 생성 에디터 도구.
/// d0 해안 → d1 숲 → d2 늪지 → d3 산지 → d4 설원 → d5 폐허 (Z+ 방향 일방향 종단).
/// 곧은 회랑(X 폭 고정, Z 종단)·경계마다 키 큰 지형벽으로 앞뒤 지역 시야차폐.
/// [확정] 밴드 경계 Z = WorldAreaInfo.csv AxisMin/AxisMax 단일 진실원. 이 도구가 CSV를 직접 파싱(하드코딩 const 금지).
/// 지형은 placeholder(기존 Synty 에셋) — 아트 교체는 슬라이스2b designer.
/// </summary>
public class MapGenerator : EditorWindow
{
    private const float CORRIDOR_HALF_WIDTH = 30f;   // 회랑 X 반폭(±30 → 폭 60). 곧은 종단.
    private const float MAX_SLOPE_DEGREE = 60f;
    private const float STEP_HEIGHT = 0.4f;
    private const float DEEP_WATER_Y = -1.5f;
    private const float GROUND_TILE_SIZE = 4.5f;

    private const int RANDOM_SEED = 20260426;

    private const string AREA_CSV_PATH = "Assets/CSVInfo/WorldAreaInfo.csv";

    // CSV에서 파싱한 6지역 밴드(단일 진실원). Build 시 LoadAreaBands()로 채움.
    private struct AreaBand
    {
        public int Id;
        public string Name;
        public float ZMin;
        public float ZMax;
    }

    // 지역 깊이 d=0..5 → 지형 성격(데코/머티리얼 placeholder 선택용)
    private enum Region { Beach, Forest, Swamp, Mountain, Snow, Ruins, OuterRim, Outside }

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

    private static readonly Dictionary<PrefabCategory, string[]> CATEGORY_PATTERNS =
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

    // CSV 파싱 결과. GenerateMap 호출마다 갱신.
    private static List<AreaBand> s_Bands = new();
    private static float s_ZMin;   // 종단축 시작(가장 얕은 지역 ZMin)
    private static float s_ZMax;   // 종단축 끝(가장 깊은 지역 ZMax)

    [MenuItem("Tools/Map Generator/Generate Island Map")]
    public static void GenerateMap()
    {
        if (!LoadAreaBands())
        {
            Debug.LogError("[MapGenerator] WorldAreaInfo.csv 파싱 실패. 맵 생성 중단.");
            return;
        }

        var oldPlane = GameObject.Find("Plane");
        if (oldPlane != null) DestroyImmediate(oldPlane);

        var mapRoot = GameObject.Find("MapRoot");
        if (mapRoot == null) mapRoot = new GameObject("MapRoot");

        var oldGenerated = mapRoot.transform.Find("GeneratedMap");
        if (oldGenerated != null) DestroyImmediate(oldGenerated.gameObject);

        var generated = new GameObject("GeneratedMap");
        generated.transform.SetParent(mapRoot.transform);

        Random.InitState(RANDOM_SEED);

        GenerateGround(generated.transform);
        GenerateSlopes(generated.transform);
        GenerateHills(generated.transform);
        GenerateOuterRim(generated.transform);
        GenerateAreaWalls(generated.transform); // 경계 시야차폐 지형벽

        // 데코 배치 전에 콜라이더 동기화 (raycast 정확도)
        Physics.SyncTransforms();

        GenerateDecorations(generated.transform);
        GenerateSkydome(generated.transform);
        GenerateClouds(generated.transform);
        GenerateBoundaryWall(generated.transform);
        SetupSpawnAndEscapePoints();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[MapGenerator] 6지역 종단 회랑 맵 생성 완료 (Z {s_ZMin}~{s_ZMax}, 폭 ±{CORRIDOR_HALF_WIDTH}, 밴드 {s_Bands.Count}개)");
    }

    [MenuItem("Tools/Map Generator/Bake NavMesh")]
    public static void BakeNavMesh()
    {
        var generated = GameObject.Find("MapRoot/GeneratedMap");
        if (generated == null)
        {
            Debug.LogError("[MapGenerator] GeneratedMap 없음. Generate Island Map 먼저 실행.");
            return;
        }

        // NavigationStatic 마킹 전략:
        // - Walkable: Ground/Slopes/Hills (회랑 안쪽 평면)
        // - Non-Walkable (자동 carving): BoundaryWall(회랑 좌우 벽), AreaWalls(경계 차폐벽), Decorations(나무/바위 등)
        ClearNavStaticRecursively(generated);
        MarkNavStaticRecursively(generated.transform.Find("Ground"));
        MarkNavStaticRecursively(generated.transform.Find("Slopes"));
        MarkNavStaticRecursively(generated.transform.Find("Hills"));
        EnsureBoundaryWallObstacles(generated.transform.Find("BoundaryWall"));
        EnsureBoundaryWallObstacles(generated.transform.Find("AreaWalls"));
        MarkNavStaticByPrefix(generated.transform.Find("Decorations"),
            new[] { "Tree_", "Rock_", "Stump_" });

        var settings = NavMesh.GetSettingsByID(0);
        settings.agentSlope = MAX_SLOPE_DEGREE;
        settings.agentClimb = STEP_HEIGHT;

        NavMesh.RemoveAllNavMeshData();
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[MapGenerator] NavMesh baked (Max Slope {MAX_SLOPE_DEGREE}°, Step {STEP_HEIGHT})");
    }

    private static void MarkNavStaticRecursively(Transform _root)
    {
        if (_root == null) return;
        foreach (Transform child in _root)
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.NavigationStatic);
    }

    private static void MarkNavStaticByPrefix(Transform _root, string[] _prefixes)
    {
        if (_root == null || _prefixes == null) return;
        foreach (Transform child in _root)
        {
            foreach (var prefix in _prefixes)
            {
                if (child.name.StartsWith(prefix))
                {
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.NavigationStatic);
                    break;
                }
            }
        }
    }

    private static void EnsureBoundaryWallObstacles(Transform _root)
    {
        if (_root == null) return;
        foreach (Transform seg in _root)
        {
            var box = seg.GetComponent<BoxCollider>();
            if (box == null) continue;
            var obstacle = seg.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = seg.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }
    }

    private static void ClearNavStaticRecursively(GameObject _root)
    {
        if (_root == null) return;
        var flags = GameObjectUtility.GetStaticEditorFlags(_root);
        if ((flags & StaticEditorFlags.NavigationStatic) != 0)
            GameObjectUtility.SetStaticEditorFlags(_root, flags & ~StaticEditorFlags.NavigationStatic);
        foreach (Transform child in _root.transform)
            ClearNavStaticRecursively(child.gameObject);
    }

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

        Assert(LoadAreaBands(), "WorldAreaInfo.csv 파싱 성공");
        Assert(s_Bands.Count == 6, $"6개 밴드 파싱 (실제 {s_Bands.Count})");

        Assert(IsInsideCorridor(0, s_ZMin + 5f), "회랑 시작부 안쪽");
        Assert(IsInsideCorridor(0, s_ZMax - 5f), "회랑 끝부 안쪽");
        Assert(!IsInsideCorridor(CORRIDOR_HALF_WIDTH + 10f, 0), "회랑 X 폭 밖");
        Assert(!IsInsideCorridor(0, s_ZMin - 10f), "회랑 종단 시작 밖");
        Assert(!IsInsideCorridor(0, s_ZMax + 10f), "회랑 종단 끝 밖");

        // 밴드 경계가 CSV와 일치하는지(d별 대표 Z)
        Assert(GetRegion(0, -50) == Region.Beach, "(0,-50) Beach");
        Assert(GetRegion(0, 10) == Region.Forest, "(0,10) Forest");
        Assert(GetRegion(0, 50) == Region.Swamp, "(0,50) Swamp");
        Assert(GetRegion(0, 100) == Region.Mountain, "(0,100) Mountain");
        Assert(GetRegion(0, 140) == Region.Snow, "(0,140) Snow");
        Assert(GetRegion(0, 180) == Region.Ruins, "(0,180) Ruins");

        Assert(FindPrefabsByCategory(PrefabCategory.GroundFlat).Length > 0, "GroundFlat 풀 비어있지 않음");
        Assert(FindPrefabsByCategory(PrefabCategory.Tree).Length > 0, "Tree 풀 비어있지 않음");
        Assert(FindPrefabsByCategory(PrefabCategory.Skydome).Length > 0, "Skydome 1종 이상");

        Debug.Log($"[Validate] 결과: PASS {passed}, FAIL {failed}");
    }

    // ==================== CSV 단일 진실원 파싱 ====================

    // WorldAreaInfo.csv를 직접 파싱(에디터 도구는 InfoManager 런타임 미로드).
    // 헤더: Id.INT,Name.STRING,MaxCount.INT,RespawnDelay.FLOAT,MoveCostMultiplier.FLOAT,AxisMin.FLOAT,AxisMax.FLOAT
    private static bool LoadAreaBands()
    {
        s_Bands = new List<AreaBand>();

        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AREA_CSV_PATH);
        string text = asset != null ? asset.text : null;
        if (string.IsNullOrEmpty(text) && System.IO.File.Exists(AREA_CSV_PATH))
            text = System.IO.File.ReadAllText(AREA_CSV_PATH);
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogError($"[MapGenerator] {AREA_CSV_PATH} 읽기 실패");
            return false;
        }

        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        if (lines.Length < 2) return false;

        // 헤더에서 컬럼 인덱스 해석(컬럼 순서 변경에 견고)
        string[] header = lines[0].Split(',');
        int idxId = FindColumn(header, "Id");
        int idxName = FindColumn(header, "Name");
        int idxMin = FindColumn(header, "AxisMin");
        int idxMax = FindColumn(header, "AxisMax");
        if (idxId < 0 || idxMin < 0 || idxMax < 0)
        {
            Debug.LogError("[MapGenerator] WorldAreaInfo.csv 헤더에 Id/AxisMin/AxisMax 컬럼 없음");
            return false;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cols = lines[i].Split(',');
            if (cols.Length <= Mathf.Max(idxId, Mathf.Max(idxMin, idxMax))) continue;

            var band = new AreaBand
            {
                Id = ParseInt(cols[idxId]),
                Name = idxName >= 0 && idxName < cols.Length ? cols[idxName] : "",
                ZMin = ParseFloat(cols[idxMin]),
                ZMax = ParseFloat(cols[idxMax]),
            };
            s_Bands.Add(band);
        }

        if (s_Bands.Count == 0) return false;

        s_Bands.Sort((a, b) => a.ZMin.CompareTo(b.ZMin));
        s_ZMin = s_Bands[0].ZMin;
        s_ZMax = s_Bands[s_Bands.Count - 1].ZMax;
        return true;
    }

    private static int FindColumn(string[] _header, string _name)
    {
        for (int i = 0; i < _header.Length; i++)
        {
            string h = _header[i].Trim();
            int dot = h.IndexOf('.');
            if (dot >= 0) h = h.Substring(0, dot); // "AxisMin.FLOAT" → "AxisMin"
            if (h == _name) return i;
        }
        return -1;
    }

    private static int ParseInt(string _s) =>
        int.TryParse(_s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static float ParseFloat(string _s) =>
        float.TryParse(_s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    // 종단 Z → Region(데코/머티리얼 성격). 밴드 인덱스(깊이 d) 기반.
    private static int GetBandIndex(float _z)
    {
        for (int i = 0; i < s_Bands.Count; i++)
        {
            if (_z >= s_Bands[i].ZMin && _z < s_Bands[i].ZMax)
                return i;
        }
        if (_z < s_ZMin) return -1;
        return s_Bands.Count - 1; // 끝 초과 → 가장 깊은 밴드
    }

    // ==================== 영역별 생성 ====================

    private static void GenerateGround(Transform _parent)
    {
        var groundRoot = new GameObject("Ground");
        groundRoot.transform.SetParent(_parent);

        var allFlat = FindPrefabsByCategory(PrefabCategory.GroundFlat);
        var grassPool = System.Array.FindAll(allFlat, p => p.name.Contains("Grass"));
        var dirtPool = System.Array.FindAll(allFlat, p => p.name.Contains("Dirt"));
        if (grassPool.Length == 0) grassPool = allFlat;
        if (dirtPool.Length == 0) dirtPool = allFlat;
        if (grassPool.Length == 0)
        {
            Debug.LogWarning("[MapGenerator] GroundFlat 풀 없음");
            return;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");

        for (float x = -CORRIDOR_HALF_WIDTH; x <= CORRIDOR_HALF_WIDTH; x += GROUND_TILE_SIZE)
        {
            for (float z = s_ZMin; z <= s_ZMax; z += GROUND_TILE_SIZE)
            {
                if (!IsInsideCorridor(x, z)) continue;

                int band = GetBandIndex(z);
                // 흙 계열 지역(늪지/산지/설원/폐허 = d>=2)은 dirt, 해안/숲은 grass placeholder
                GameObject prefab = band >= 2
                    ? dirtPool[Random.Range(0, dirtPool.Length)]
                    : grassPool[Random.Range(0, grassPool.Length)];

                var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                tile.transform.SetParent(groundRoot.transform);
                tile.transform.position = new Vector3(x, 0, z);
                if (groundLayer >= 0) tile.layer = groundLayer;
            }
        }
    }

    private static void GenerateSlopes(Transform _parent)
    {
        var slopeRoot = new GameObject("Slopes");
        slopeRoot.transform.SetParent(_parent);

        var slopePool = FindPrefabsByCategory(PrefabCategory.GroundSlope);
        if (slopePool.Length == 0)
        {
            Debug.LogWarning("[MapGenerator] Slope 프리팹 없음");
            return;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");

        // 밴드별 슬로프 밀도(깊은 지역일수록 험준)
        float[] slopeDensity = { 0.05f, 0.12f, 0.15f, 0.30f, 0.20f, 0.18f };
        for (int b = 0; b < s_Bands.Count; b++)
        {
            float density = b < slopeDensity.Length ? slopeDensity[b] : 0.15f;
            float zLen = s_Bands[b].ZMax - s_Bands[b].ZMin;
            int count = Mathf.RoundToInt(zLen * CORRIDOR_HALF_WIDTH * 0.02f * density * 10f);
            PlaceSlopesInRegion(slopeRoot.transform, slopePool, s_Bands[b].ZMin, s_Bands[b].ZMax, count, groundLayer);
        }
    }

    private static void PlaceSlopesInRegion(Transform _root, GameObject[] _pool, float _zMin, float _zMax, int _count, int _groundLayer)
    {
        int placed = 0;
        int attempts = 0;
        while (placed < _count && attempts < _count * 8 + 8)
        {
            attempts++;
            float x = Random.Range(-CORRIDOR_HALF_WIDTH, CORRIDOR_HALF_WIDTH);
            float z = Random.Range(_zMin, _zMax);
            if (!IsInsideCorridor(x, z)) continue;

            var prefab = _pool[Random.Range(0, _pool.Length)];
            var slope = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            slope.transform.SetParent(_root);
            slope.transform.position = new Vector3(x, 0.05f, z);
            slope.transform.rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0);
            if (_groundLayer >= 0) slope.layer = _groundLayer;
            placed++;
        }
    }

    private static void GenerateHills(Transform _parent)
    {
        var hillRoot = new GameObject("Hills");
        hillRoot.transform.SetParent(_parent);

        var hillPool = FindPrefabsByCategory(PrefabCategory.Hill);
        if (hillPool.Length == 0) return;

        int groundLayer = LayerMask.NameToLayer("Ground");

        // 산지(d3)에 언덕 집중, 그 외 소량
        for (int b = 0; b < s_Bands.Count; b++)
        {
            int count = b == 3 ? Random.Range(3, 5) : Random.Range(0, 2);
            PlaceHills(hillRoot.transform, hillPool, s_Bands[b].ZMin, s_Bands[b].ZMax, count, groundLayer);
        }
    }

    private static void PlaceHills(Transform _root, GameObject[] _pool, float _zMin, float _zMax, int _count, int _groundLayer)
    {
        int placed = 0;
        int attempts = 0;
        while (placed < _count && attempts < _count * 12 + 8)
        {
            attempts++;
            float x = Random.Range(-CORRIDOR_HALF_WIDTH + 8f, CORRIDOR_HALF_WIDTH - 8f);
            float z = Random.Range(_zMin, _zMax);
            if (!IsInsideCorridor(x, z)) continue;

            var prefab = _pool[Random.Range(0, _pool.Length)];
            var hill = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            hill.transform.SetParent(_root);
            hill.transform.position = new Vector3(x, 0, z);
            hill.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            hill.transform.localScale = Vector3.one * Random.Range(0.9f, 1.4f);
            if (_groundLayer >= 0) hill.layer = _groundLayer;
            placed++;
        }
    }

    // 회랑 좌우(X 양끝)를 따라 절벽/물/산배경으로 종단 경계 시각화.
    private static void GenerateOuterRim(Transform _parent)
    {
        var rimRoot = new GameObject("OuterRim");
        rimRoot.transform.SetParent(_parent);

        var cliffPool = FindPrefabsByCategory(PrefabCategory.Cliff);
        var waterPool = FindPrefabsByCategory(PrefabCategory.Water);
        var mountainPool = FindPrefabsByCategory(PrefabCategory.MountainBackdrop);

        if (cliffPool.Length > 0)
        {
            for (float z = s_ZMin; z <= s_ZMax; z += 8f)
            {
                PlaceCliffEdge(rimRoot.transform, cliffPool, -CORRIDOR_HALF_WIDTH - 2f, z, 1f);  // 좌측
                PlaceCliffEdge(rimRoot.transform, cliffPool, CORRIDOR_HALF_WIDTH + 2f, z, -1f);   // 우측
            }
        }

        // 깊은 물 평면(시작부 해안 쪽)
        if (waterPool.Length > 0)
        {
            var deepWater = (GameObject)PrefabUtility.InstantiatePrefab(waterPool[0]);
            deepWater.transform.SetParent(rimRoot.transform);
            deepWater.transform.position = new Vector3(0, DEEP_WATER_Y, s_ZMin - 10f);
            deepWater.transform.localScale = Vector3.one * 20f;
            deepWater.name = "DeepWater";
        }

        // 종단 끝(폐허 너머) 산 배경
        if (mountainPool.Length > 0)
        {
            var bg = (GameObject)PrefabUtility.InstantiatePrefab(mountainPool[Random.Range(0, mountainPool.Length)]);
            bg.transform.SetParent(rimRoot.transform);
            bg.transform.position = new Vector3(0f, 0, s_ZMax + 18f);
            bg.transform.localScale = Vector3.one * 2.2f;
            bg.name = "MountainBackdrop_End";
        }
    }

    private static void PlaceCliffEdge(Transform _root, GameObject[] _pool, float _x, float _z, float _faceDir)
    {
        var cliff = (GameObject)PrefabUtility.InstantiatePrefab(_pool[Random.Range(0, _pool.Length)]);
        cliff.transform.SetParent(_root);
        cliff.transform.position = new Vector3(_x, 0, _z);
        cliff.transform.rotation = Quaternion.LookRotation(new Vector3(_faceDir, 0, 0));
    }

    // 경계마다 키 큰 지형벽(시야차폐). 통과 회랑(가운데 X 일부)만 비워 일방향 진행로 유지.
    // [확정] 1차 = 곧은 회랑 + 키 큰 지형벽 시야차폐. S/L자 굴곡은 2차 백로그.
    private static void GenerateAreaWalls(Transform _parent)
    {
        var wallRoot = new GameObject("AreaWalls");
        wallRoot.transform.SetParent(_parent);

        var mountainPool = FindPrefabsByCategory(PrefabCategory.MountainBackdrop);
        var cliffPool = FindPrefabsByCategory(PrefabCategory.Cliff);
        const float GAP_HALF = 9f;        // 통과 통로 반폭(가운데 비움)
        const float WALL_HEIGHT = 14f;    // 시야차폐 높이

        // 내부 경계만(시작/끝 제외) — s_Bands[1..n-1].ZMin
        for (int b = 1; b < s_Bands.Count; b++)
        {
            float z = s_Bands[b].ZMin;

            // 좌/우 차폐 지형(장식 — 산/절벽). 가운데 GAP은 비움.
            PlaceWallDecor(wallRoot.transform, mountainPool, cliffPool, -CORRIDOR_HALF_WIDTH * 0.5f - 5f, z);
            PlaceWallDecor(wallRoot.transform, mountainPool, cliffPool, CORRIDOR_HALF_WIDTH * 0.5f + 5f, z);

            // 시야차폐 콜라이더 벽(좌/우, 가운데 GAP 비움) — NavMesh carve + 물리 차단
            CreateWallSegment(wallRoot.transform, $"AreaWall_{b}_L",
                new Vector3(-(CORRIDOR_HALF_WIDTH + GAP_HALF) / 2f, WALL_HEIGHT / 2f, z),
                new Vector3(CORRIDOR_HALF_WIDTH - GAP_HALF, WALL_HEIGHT, 2f));
            CreateWallSegment(wallRoot.transform, $"AreaWall_{b}_R",
                new Vector3((CORRIDOR_HALF_WIDTH + GAP_HALF) / 2f, WALL_HEIGHT / 2f, z),
                new Vector3(CORRIDOR_HALF_WIDTH - GAP_HALF, WALL_HEIGHT, 2f));
        }
    }

    private static void PlaceWallDecor(Transform _root, GameObject[] _mountainPool, GameObject[] _cliffPool, float _x, float _z)
    {
        GameObject[] pool = _mountainPool.Length > 0 ? _mountainPool : _cliffPool;
        if (pool.Length == 0) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pool[Random.Range(0, pool.Length)]);
        go.transform.SetParent(_root);
        go.transform.position = new Vector3(_x, 0, _z);
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        go.transform.localScale = Vector3.one * Random.Range(1.0f, 1.5f);
        go.name = $"WallDecor_{_z:F0}";
    }

    private static void CreateWallSegment(Transform _root, string _name, Vector3 _center, Vector3 _size)
    {
        var seg = new GameObject(_name);
        seg.transform.SetParent(_root);
        seg.transform.position = _center;
        var box = seg.AddComponent<BoxCollider>();
        box.size = _size;
    }

    // ==================== 데코 ====================

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

    // 밴드 b의 Z범위 반환(없으면 false)
    private static bool TryBandZ(int _b, out float _zMin, out float _zMax)
    {
        if (_b >= 0 && _b < s_Bands.Count)
        {
            _zMin = s_Bands[_b].ZMin;
            _zMax = s_Bands[_b].ZMax;
            return true;
        }
        _zMin = _zMax = 0f;
        return false;
    }

    private static void PlaceTreesByRegion(Transform _root)
    {
        var treePool = FindPrefabsByCategory(PrefabCategory.Tree);
        if (treePool.Length == 0) return;

        var deadPool = System.Array.FindAll(treePool, p => p.name.Contains("Dead"));
        var nonDeadPool = System.Array.FindAll(treePool, p => !p.name.Contains("Dead"));
        if (nonDeadPool.Length == 0) nonDeadPool = treePool;
        var deadOrAny = deadPool.Length > 0 ? deadPool : treePool;

        // d0 해안: 소량, d1 숲: 밀집, d2 늪지: 보통(고사목 섞임), d3 산지: 고사목 소량, d4 설원: 희박 고사목, d5 폐허: 희박 고사목
        if (TryBandZ(0, out float z0a, out float z0b)) PlaceCluster(_root, nonDeadPool, z0a + 8f, z0b, 7, 5f, "Tree_Beach", 0.9f, 1.3f);
        if (TryBandZ(1, out float z1a, out float z1b)) PlaceCluster(_root, nonDeadPool, z1a, z1b, 26, 3.5f, "Tree_Forest", 0.9f, 1.4f);
        if (TryBandZ(2, out float z2a, out float z2b)) PlaceCluster(_root, deadOrAny, z2a, z2b, 14, 4f, "Tree_Swamp", 0.8f, 1.3f);
        if (TryBandZ(3, out float z3a, out float z3b)) PlaceCluster(_root, deadOrAny, z3a, z3b, 5, 4f, "Tree_Mountain", 0.8f, 1.2f);
        if (TryBandZ(4, out float z4a, out float z4b)) PlaceCluster(_root, deadOrAny, z4a, z4b, 4, 5f, "Tree_Snow", 0.7f, 1.1f);
        if (TryBandZ(5, out float z5a, out float z5b)) PlaceCluster(_root, deadOrAny, z5a, z5b, 5, 5f, "Tree_Ruins", 0.7f, 1.1f);
    }

    private static void PlaceBushesByRegion(Transform _root)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Bush);
        if (pool.Length == 0) return;
        if (TryBandZ(1, out float z1a, out float z1b)) PlaceCluster(_root, pool, z1a, z1b, 16, 2f, "Bush_Forest", 0.9f, 1.3f);
        if (TryBandZ(2, out float z2a, out float z2b)) PlaceCluster(_root, pool, z2a, z2b, 10, 2.5f, "Bush_Swamp", 0.9f, 1.3f);
    }

    private static void PlaceRocksByRegion(Transform _root)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Rock);
        if (pool.Length == 0) return;
        if (TryBandZ(0, out float z0a, out float z0b)) PlaceCluster(_root, pool, z0a, z0b, 7, 3f, "Rock_Beach", 0.7f, 1.4f);
        if (TryBandZ(3, out float z3a, out float z3b)) PlaceCluster(_root, pool, z3a, z3b, 14, 3f, "Rock_Mountain", 0.9f, 1.6f);
        if (TryBandZ(4, out float z4a, out float z4b)) PlaceCluster(_root, pool, z4a, z4b, 8, 3.5f, "Rock_Snow", 0.9f, 1.5f);
        if (TryBandZ(5, out float z5a, out float z5b)) PlaceCluster(_root, pool, z5a, z5b, 10, 3f, "Rock_Ruins", 0.8f, 1.5f);
    }

    private static void PlaceGrassByRegion(Transform _root)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Grass);
        if (pool.Length == 0) return;
        // 해안~늪지(d0~2)만 풀
        if (TryBandZ(0, out float za, out _) && TryBandZ(2, out _, out float zb))
            PlaceCluster(_root, pool, za, zb, 50, 1.2f, "Grass_Low", 0.8f, 1.2f);
    }

    private static void PlaceFlowersByRegion(Transform _root)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Flower);
        if (pool.Length == 0) return;
        if (TryBandZ(1, out float z1a, out float z1b)) PlaceCluster(_root, pool, z1a, z1b, 16, 1.5f, "Flower_Forest", 0.9f, 1.2f);
    }

    private static void PlaceMushroomsAndStumps(Transform _root)
    {
        var mushPool = FindPrefabsByCategory(PrefabCategory.Mushroom);
        if (mushPool.Length > 0 && TryBandZ(2, out float z2a, out float z2b))
            PlaceCluster(_root, mushPool, z2a, z2b, 10, 1.2f, "Mush_Swamp", 0.9f, 1.3f);
        var stumpPool = FindPrefabsByCategory(PrefabCategory.Stump);
        if (stumpPool.Length > 0 && TryBandZ(1, out float z1a, out float z1b))
            PlaceCluster(_root, stumpPool, z1a, z1b, 3, 8f, "Stump_Forest", 0.9f, 1.2f);
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

    private static List<Vector2> PickPoissonPoints(float _zMin, float _zMax, float _minDistance, int _maxCount, int _attempts = 30)
    {
        var points = new List<Vector2>();
        int tries = 0;
        while (points.Count < _maxCount && tries < _maxCount * _attempts)
        {
            tries++;
            float x = Random.Range(-CORRIDOR_HALF_WIDTH, CORRIDOR_HALF_WIDTH);
            float z = Random.Range(_zMin, _zMax);
            if (!IsInsideCorridor(x, z)) continue;

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

    // ==================== Skydome / Cloud / Boundary ====================

    private static void GenerateSkydome(Transform _parent)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Skydome);
        if (pool.Length == 0) return;

        var sky = (GameObject)PrefabUtility.InstantiatePrefab(pool[0]);
        sky.transform.SetParent(_parent);
        sky.transform.position = new Vector3(0, 0, (s_ZMin + s_ZMax) / 2f);
        sky.transform.localScale = Vector3.one * 6f;
        sky.name = "Skydome";
    }

    private static void GenerateClouds(Transform _parent)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Cloud);
        if (pool.Length == 0) return;

        var cloudRoot = new GameObject("Clouds");
        cloudRoot.transform.SetParent(_parent);

        const int CLOUD_COUNT = 8;
        for (int i = 0; i < CLOUD_COUNT; i++)
        {
            float z = Mathf.Lerp(s_ZMin, s_ZMax, (i + 0.5f) / CLOUD_COUNT) + Random.Range(-10f, 10f);
            float x = Random.Range(-CORRIDOR_HALF_WIDTH, CORRIDOR_HALF_WIDTH);
            float y = Random.Range(35f, 50f);

            var prefab = pool[Random.Range(0, pool.Length)];
            var cloud = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            cloud.transform.SetParent(cloudRoot.transform);
            cloud.transform.position = new Vector3(x, y, z);
            cloud.transform.localScale = Vector3.one * Random.Range(1.5f, 2.5f);
            cloud.name = $"Cloud_{i}";
        }
    }

    // 회랑 좌우 종단 벽 + 시작/끝 마개. 일방향 종단 경계.
    private static void GenerateBoundaryWall(Transform _parent)
    {
        var wallRoot = new GameObject("BoundaryWall");
        wallRoot.transform.SetParent(_parent);

        const float WALL_HEIGHT = 6f;
        const float WALL_THICKNESS = 2f;
        float zLen = s_ZMax - s_ZMin;
        float zMid = (s_ZMin + s_ZMax) / 2f;
        float wallX = CORRIDOR_HALF_WIDTH + WALL_THICKNESS / 2f;

        // 좌우 종단 벽
        CreateBoundarySegment(wallRoot.transform, "WallLeft",
            new Vector3(-wallX, WALL_HEIGHT / 2f, zMid),
            new Vector3(WALL_THICKNESS, WALL_HEIGHT, zLen + 4f));
        CreateBoundarySegment(wallRoot.transform, "WallRight",
            new Vector3(wallX, WALL_HEIGHT / 2f, zMid),
            new Vector3(WALL_THICKNESS, WALL_HEIGHT, zLen + 4f));
        // 시작/끝 마개
        CreateBoundarySegment(wallRoot.transform, "WallStart",
            new Vector3(0, WALL_HEIGHT / 2f, s_ZMin - WALL_THICKNESS / 2f),
            new Vector3(CORRIDOR_HALF_WIDTH * 2f + WALL_THICKNESS * 2f, WALL_HEIGHT, WALL_THICKNESS));
        CreateBoundarySegment(wallRoot.transform, "WallEnd",
            new Vector3(0, WALL_HEIGHT / 2f, s_ZMax + WALL_THICKNESS / 2f),
            new Vector3(CORRIDOR_HALF_WIDTH * 2f + WALL_THICKNESS * 2f, WALL_HEIGHT, WALL_THICKNESS));
    }

    private static void CreateBoundarySegment(Transform _root, string _name, Vector3 _center, Vector3 _size)
    {
        var seg = new GameObject(_name);
        seg.transform.SetParent(_root);
        seg.transform.position = _center;

        var box = seg.AddComponent<BoxCollider>();
        box.size = _size;

        var obstacle = seg.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = box.center;
        obstacle.size = box.size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    // ==================== Spawn / Escape / Area ====================

    private static void SetupSpawnAndEscapePoints()
    {
        // 시작 = 해안(d0) 시작부
        float startZ = s_ZMin + 8f;
        MoveObject("StartPosition1", new Vector3(-3f, 0f, startZ));
        MoveObject("StartPosition2", new Vector3(3f, 0f, startZ));
        MoveObject("StartPosition3", new Vector3(-6f, 0f, startZ + 4f));
        MoveObject("StartPosition4", new Vector3(6f, 0f, startZ + 4f));
        // 탈출 = 종단 끝(폐허 너머 마을)
        MoveObject("EscapePoint", new Vector3(0f, 0f, s_ZMax - 5f));

        // 6지역 스폰 영역(각 밴드 중앙). 기존 Area1~3 이름 유지 + 신규 4~6 생성 시도.
        string[] areaNames = { "Area1_Beach", "Area2_Forest", "Area3_Swamp", "Area4_Mountain", "Area5_Snow", "Area6_Ruins" };
        for (int b = 0; b < s_Bands.Count && b < areaNames.Length; b++)
        {
            float zc = (s_Bands[b].ZMin + s_Bands[b].ZMax) / 2f;
            MoveOrCreateAreaMarker(areaNames[b], new Vector3(0f, 0f, zc), b + 1);
        }
    }

    private static void MoveObject(string _name, Vector3 _position)
    {
        var go = GameObject.Find(_name);
        if (go != null) go.transform.position = _position;
    }

    // 스폰 영역 마커: 있으면 이동, 없으면 placeholder GameObject 생성(MonsterSpawnArea 부착은 level-design/씬 작업).
    private static void MoveOrCreateAreaMarker(string _name, Vector3 _position, int _areaId)
    {
        var go = GameObject.Find(_name);
        if (go == null)
        {
            go = new GameObject(_name);
            Debug.Log($"[MapGenerator] 신규 영역 마커 생성: {_name} (AreaId {_areaId}). MonsterSpawnArea 컴포넌트/InGameAreaWorker 등록은 씬 작업 필요.");
        }
        go.transform.position = _position;
    }

    // ==================== 헬퍼 ====================

    // 곧은 회랑: X는 ±CORRIDOR_HALF_WIDTH, Z는 종단 [s_ZMin, s_ZMax].
    private static bool IsInsideCorridor(float _x, float _z)
    {
        return _x >= -CORRIDOR_HALF_WIDTH && _x <= CORRIDOR_HALF_WIDTH
            && _z >= s_ZMin && _z <= s_ZMax;
    }

    private static Region GetRegion(float _x, float _z)
    {
        if (_x < -CORRIDOR_HALF_WIDTH || _x > CORRIDOR_HALF_WIDTH) return Region.OuterRim;
        if (_z < s_ZMin || _z > s_ZMax) return Region.Outside;

        int b = GetBandIndex(_z);
        return b switch
        {
            0 => Region.Beach,
            1 => Region.Forest,
            2 => Region.Swamp,
            3 => Region.Mountain,
            4 => Region.Snow,
            5 => Region.Ruins,
            _ => Region.Outside,
        };
    }

    private static GameObject[] FindPrefabsByCategory(PrefabCategory _category)
    {
        if (!CATEGORY_PATTERNS.TryGetValue(_category, out var patterns))
            return System.Array.Empty<GameObject>();

        var results = new List<GameObject>();
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

    private static float GetTerrainHeight(float _x, float _z)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int mask = groundLayer >= 0 ? (1 << groundLayer) : ~0;
        Ray ray = new Ray(new Vector3(_x, 200f, _z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 400f, mask))
            return hit.point.y;
        return 0f;
    }
}
#endif
