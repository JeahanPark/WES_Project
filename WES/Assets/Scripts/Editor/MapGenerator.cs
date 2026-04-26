#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.AI;

/// <summary>
/// 원형 섬 맵 자동 생성 에디터 도구
/// 해안(남) → 숲(중앙) → 산지(북) 영역 구성
/// </summary>
public class MapGenerator : EditorWindow
{
    private const float ISLAND_RADIUS = 75f;
    private const float PLAYABLE_RADIUS = 70f;
    private const float MAX_SLOPE_DEGREE = 60f;
    private const float STEP_HEIGHT = 0.4f;
    private const float WATER_Y = -0.3f;
    private const float DEEP_WATER_Y = -1.5f;
    private const float GROUND_TILE_SIZE = 4.5f;

    private const float BEACH_Z_MAX = -10f;
    private const float FOREST_Z_MAX = 30f;

    private const int RANDOM_SEED = 20260426;

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

        Random.InitState(RANDOM_SEED);

        GenerateGround(generated.transform);
        GenerateSlopes(generated.transform);
        GenerateHills(generated.transform);
        GenerateOuterRim(generated.transform);

        // 데코 배치 전에 콜라이더 동기화 (raycast 정확도)
        Physics.SyncTransforms();

        GenerateDecorations(generated.transform);
        GenerateSkydome(generated.transform);
        GenerateClouds(generated.transform);
        GenerateBoundaryWall(generated.transform);
        SetupSpawnAndEscapePoints();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[MapGenerator] Island map generated!");
    }

    [MenuItem("Tools/Map Generator/Bake NavMesh")]
    public static void BakeNavMesh()
    {
        var ground = GameObject.Find("MapRoot/GeneratedMap/Ground");
        if (ground != null)
        {
            foreach (Transform child in ground.transform)
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.NavigationStatic);
        }

        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[MapGenerator] NavMesh baked!");
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

        Assert(IsInsideIsland(0, 0), "원점은 섬 안");
        Assert(IsInsideIsland(60, 0), "(60, 0)은 섬 안 (PLAYABLE_RADIUS=70)");
        Assert(!IsInsideIsland(80, 0), "(80, 0)은 섬 밖");
        Assert(!IsInsideIsland(50, 50), "(50, 50)은 거리 70.7로 섬 밖");

        Assert(GetRegion(0, -50) == Region.Beach, "(0, -50)은 Beach");
        Assert(GetRegion(0, 0) == Region.Forest, "(0, 0)은 Forest");
        Assert(GetRegion(0, 50) == Region.Mountain, "(0, 50)은 Mountain");
        Assert(GetRegion(0, 72) == Region.OuterRim, "(0, 72)는 OuterRim");
        Assert(GetRegion(0, 80) == Region.Outside, "(0, 80)은 Outside");

        Assert(FindPrefabsByCategory(PrefabCategory.GroundFlat).Length > 0, "GroundFlat 풀 비어있지 않음");
        Assert(FindPrefabsByCategory(PrefabCategory.GroundSlope).Length >= 2, "GroundSlope 2종 이상");
        Assert(FindPrefabsByCategory(PrefabCategory.Hill).Length >= 2, "Hill 2종 이상");
        Assert(FindPrefabsByCategory(PrefabCategory.Tree).Length > 0, "Tree 풀 비어있지 않음");
        Assert(FindPrefabsByCategory(PrefabCategory.Skydome).Length > 0, "Skydome 1종 이상");

        Debug.Log($"[Validate] 결과: PASS {passed}, FAIL {failed}");
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

        for (float x = -PLAYABLE_RADIUS; x <= PLAYABLE_RADIUS; x += GROUND_TILE_SIZE)
        {
            for (float z = -PLAYABLE_RADIUS; z <= PLAYABLE_RADIUS; z += GROUND_TILE_SIZE)
            {
                if (!IsInsideIsland(x, z)) continue;

                Region region = GetRegion(x, z);
                GameObject prefab = region == Region.Mountain
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

        // 숲: 15%, 산지: 50%
        PlaceSlopesInRegion(slopeRoot.transform, slopePool, BEACH_Z_MAX, FOREST_Z_MAX,
            Mathf.RoundToInt(GetRegionTileApproxCount(Region.Forest) * 0.15f), groundLayer);
        PlaceSlopesInRegion(slopeRoot.transform, slopePool, FOREST_Z_MAX, PLAYABLE_RADIUS,
            Mathf.RoundToInt(GetRegionTileApproxCount(Region.Mountain) * 0.5f), groundLayer);
    }

    private static void PlaceSlopesInRegion(Transform _root, GameObject[] _pool, float _zMin, float _zMax, int _count, int _groundLayer)
    {
        int placed = 0;
        int attempts = 0;
        while (placed < _count && attempts < _count * 8)
        {
            attempts++;
            float x = Random.Range(-PLAYABLE_RADIUS, PLAYABLE_RADIUS);
            float z = Random.Range(_zMin, _zMax);
            if (!IsInsideIsland(x, z)) continue;

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

        PlaceHills(hillRoot.transform, hillPool, BEACH_Z_MAX, FOREST_Z_MAX, Random.Range(1, 3), groundLayer);
        PlaceHills(hillRoot.transform, hillPool, FOREST_Z_MAX, PLAYABLE_RADIUS, Random.Range(3, 5), groundLayer);
    }

    private static void PlaceHills(Transform _root, GameObject[] _pool, float _zMin, float _zMax, int _count, int _groundLayer)
    {
        int placed = 0;
        int attempts = 0;
        while (placed < _count && attempts < _count * 12)
        {
            attempts++;
            float x = Random.Range(-PLAYABLE_RADIUS + 10f, PLAYABLE_RADIUS - 10f);
            float z = Random.Range(_zMin, _zMax);
            if (!IsInsideIsland(x, z)) continue;

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

    private static void GenerateOuterRim(Transform _parent)
    {
        var rimRoot = new GameObject("OuterRim");
        rimRoot.transform.SetParent(_parent);

        var allFlat = FindPrefabsByCategory(PrefabCategory.GroundFlat);
        var sandPool = System.Array.FindAll(allFlat, p => p.name.Contains("Dirt"));
        if (sandPool.Length == 0) sandPool = allFlat;
        var cliffPool = FindPrefabsByCategory(PrefabCategory.Cliff);
        var waterPool = FindPrefabsByCategory(PrefabCategory.Water);
        var mountainPool = FindPrefabsByCategory(PrefabCategory.MountainBackdrop);

        int groundLayer = LayerMask.NameToLayer("Ground");

        for (float angle = 0; angle < 360f; angle += 6f)
        {
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad) * (PLAYABLE_RADIUS + 2f);
            float z = Mathf.Sin(rad) * (PLAYABLE_RADIUS + 2f);

            if (z < BEACH_Z_MAX)
            {
                if (sandPool.Length > 0)
                {
                    var sand = (GameObject)PrefabUtility.InstantiatePrefab(sandPool[Random.Range(0, sandPool.Length)]);
                    sand.transform.SetParent(rimRoot.transform);
                    sand.transform.position = new Vector3(x, -0.1f, z);
                    if (groundLayer >= 0) sand.layer = groundLayer;
                }
            }
            else if (z > FOREST_Z_MAX)
            {
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

        // 깊은 물 평면
        if (waterPool.Length > 0)
        {
            var deepWater = (GameObject)PrefabUtility.InstantiatePrefab(waterPool[0]);
            deepWater.transform.SetParent(rimRoot.transform);
            deepWater.transform.position = new Vector3(0, DEEP_WATER_Y, 0);
            deepWater.transform.localScale = Vector3.one * 30f;
            deepWater.name = "DeepWater";
        }

        // 산 배경 (북쪽)
        if (mountainPool.Length > 0)
        {
            var bg1 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPool[Random.Range(0, mountainPool.Length)]);
            bg1.transform.SetParent(rimRoot.transform);
            bg1.transform.position = new Vector3(-25f, 0, ISLAND_RADIUS + 15f);
            bg1.transform.localScale = Vector3.one * 1.8f;
            bg1.name = "MountainBackdrop_Left";

            var bg2 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPool[Random.Range(0, mountainPool.Length)]);
            bg2.transform.SetParent(rimRoot.transform);
            bg2.transform.position = new Vector3(25f, 0, ISLAND_RADIUS + 12f);
            bg2.transform.localScale = Vector3.one * 1.5f;
            bg2.name = "MountainBackdrop_Right";
        }
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

    private static void PlaceTreesByRegion(Transform _root)
    {
        var treePool = FindPrefabsByCategory(PrefabCategory.Tree);
        if (treePool.Length == 0) return;

        var deadPool = System.Array.FindAll(treePool, p => p.name.Contains("Dead"));
        var nonDeadPool = System.Array.FindAll(treePool, p => !p.name.Contains("Dead"));
        if (nonDeadPool.Length == 0) nonDeadPool = treePool;

        PlaceCluster(_root, nonDeadPool, -PLAYABLE_RADIUS + 10f, BEACH_Z_MAX, 9, 5f, "Tree_Beach", 0.9f, 1.3f);
        PlaceCluster(_root, nonDeadPool, BEACH_Z_MAX, FOREST_Z_MAX, 28, 3.5f, "Tree_Forest", 0.9f, 1.4f);
        if (deadPool.Length > 0)
            PlaceCluster(_root, deadPool, FOREST_Z_MAX, PLAYABLE_RADIUS, 5, 4f, "Tree_Mountain", 0.8f, 1.2f);
    }

    private static void PlaceBushesByRegion(Transform _root)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Bush);
        if (pool.Length == 0) return;
        PlaceCluster(_root, pool, BEACH_Z_MAX, FOREST_Z_MAX, 18, 2f, "Bush_Forest", 0.9f, 1.3f);
    }

    private static void PlaceRocksByRegion(Transform _root)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Rock);
        if (pool.Length == 0) return;
        PlaceCluster(_root, pool, -PLAYABLE_RADIUS, BEACH_Z_MAX, 8, 3f, "Rock_Beach", 0.7f, 1.4f);
        PlaceCluster(_root, pool, FOREST_Z_MAX, PLAYABLE_RADIUS, 12, 3f, "Rock_Mountain", 0.9f, 1.6f);
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
            PlaceCluster(_root, mushPool, -10f, 25f, 8, 1.2f, "Mush_Forest", 0.9f, 1.3f);
        var stumpPool = FindPrefabsByCategory(PrefabCategory.Stump);
        if (stumpPool.Length > 0)
            PlaceCluster(_root, stumpPool, BEACH_Z_MAX, FOREST_Z_MAX, 3, 8f, "Stump_Forest", 0.9f, 1.2f);
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

    private static int GetRegionTileApproxCount(Region _region)
    {
        return _region switch
        {
            Region.Beach => 130,
            Region.Forest => 180,
            Region.Mountain => 130,
            _ => 0,
        };
    }

    // ==================== Skydome / Cloud / Boundary ====================

    private static void GenerateSkydome(Transform _parent)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Skydome);
        if (pool.Length == 0) return;

        var sky = (GameObject)PrefabUtility.InstantiatePrefab(pool[0]);
        sky.transform.SetParent(_parent);
        sky.transform.position = Vector3.zero;
        sky.transform.localScale = Vector3.one * 4f;
        sky.name = "Skydome";
    }

    private static void GenerateClouds(Transform _parent)
    {
        var pool = FindPrefabsByCategory(PrefabCategory.Cloud);
        if (pool.Length == 0) return;

        var cloudRoot = new GameObject("Clouds");
        cloudRoot.transform.SetParent(_parent);

        const int CLOUD_COUNT = 4;
        for (int i = 0; i < CLOUD_COUNT; i++)
        {
            float angle = (360f / CLOUD_COUNT) * i + Random.Range(-20f, 20f);
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
            cloud.name = $"Cloud_{i}";
        }
    }

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
            float segLen = 2f * Mathf.PI * wallRadius / SEGMENTS + 0.5f;
            box.size = new Vector3(segLen, WALL_HEIGHT, WALL_THICKNESS);
        }
    }

    // ==================== Spawn / Escape / Area ====================

    private static void SetupSpawnAndEscapePoints()
    {
        MoveObject("StartPosition1", new Vector3(-3f, 0f, -42f));
        MoveObject("StartPosition2", new Vector3(3f, 0f, -42f));
        MoveObject("StartPosition3", new Vector3(-6f, 0f, -38f));
        MoveObject("StartPosition4", new Vector3(6f, 0f, -38f));
        MoveObject("EscapePoint",    new Vector3(0f, 8f, 60f));

        MoveObject("Area1_Beach",    new Vector3(0f, 0f, -35f));
        MoveObject("Area2_Forest",   new Vector3(0f, 0f, 10f));
        MoveObject("Area3_Mountain", new Vector3(0f, 0f, 50f));
    }

    private static void MoveObject(string _name, Vector3 _position)
    {
        var go = GameObject.Find(_name);
        if (go != null) go.transform.position = _position;
    }

    // ==================== 헬퍼 ====================

    private static bool IsInsideIsland(float _x, float _z)
    {
        return (_x * _x + _z * _z) <= (PLAYABLE_RADIUS * PLAYABLE_RADIUS);
    }

    private static float GetDistanceFromCenter(float _x, float _z)
    {
        return Mathf.Sqrt(_x * _x + _z * _z);
    }

    private static Region GetRegion(float _x, float _z)
    {
        float dist = GetDistanceFromCenter(_x, _z);
        if (dist > ISLAND_RADIUS) return Region.Outside;
        if (dist > PLAYABLE_RADIUS) return Region.OuterRim;

        if (_z < BEACH_Z_MAX) return Region.Beach;
        if (_z < FOREST_Z_MAX) return Region.Forest;
        return Region.Mountain;
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

    private static GameObject GetRandomPrefab(PrefabCategory _category)
    {
        var pool = FindPrefabsByCategory(_category);
        if (pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
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

    private static GameObject FindPrefab(string _name)
    {
        string[] guids = AssetDatabase.FindAssets($"{_name} t:Prefab",
            new[] { "Assets/Synty/PolygonGeneric/Prefabs" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains(_name))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }
}
#endif
