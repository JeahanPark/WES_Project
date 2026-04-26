#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.AI;

/// <summary>
/// M2 맵 자동 생성 에디터 도구
/// 해안가(남) → 숲(중앙) → 산지(북) 3영역 섬 맵 구성
/// </summary>
public class MapGenerator : EditorWindow
{
    private const float MAP_SIZE = 100f;
    private const float GROUND_TILE_SIZE = 4.5f;
    private const float HALF_MAP = MAP_SIZE / 2f;

    private const float ISLAND_RADIUS = 75f;
    private const float PLAYABLE_RADIUS = 70f;
    private const float MAX_SLOPE_DEGREE = 60f;
    private const float STEP_HEIGHT = 0.4f;
    private const float WATER_Y = -0.3f;
    private const float DEEP_WATER_Y = -1.5f;

    private const float BEACH_Z_MAX = -10f;
    private const float FOREST_Z_MAX = 30f;

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

    [MenuItem("Tools/Map Generator/Populate Forest and Mountain")]
    public static void PopulateForestAndMountain()
    {
        var root = GameObject.Find("MapRoot");
        if (root == null)
        {
            Debug.LogError("[MapGenerator] MapRoot not found!");
            return;
        }

        // 기존 Area_Forest, Area_Mountain 자식 오브젝트 제거 후 재생성
        var oldForest = root.transform.Find("Area_Forest");
        if (oldForest != null) DestroyImmediate(oldForest.gameObject);

        var oldMountain = root.transform.Find("Area_Mountain");
        if (oldMountain != null) DestroyImmediate(oldMountain.gameObject);

        // 물리 콜라이더 동기화 (에디터 레이캐스트 정확도)
        Physics.SyncTransforms();

        GenerateForestArea(root.transform);
        GenerateMountainArea(root.transform);

        // 씬 저장
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[MapGenerator] Forest and Mountain areas populated!");
    }

    [MenuItem("Tools/Map Generator/Generate Island Map")]
    public static void GenerateMap()
    {
        // 기존 Plane 제거
        var oldPlane = GameObject.Find("Plane");
        if (oldPlane != null)
            DestroyImmediate(oldPlane);

        // 기존 MapRoot 제거
        var oldMap = GameObject.Find("MapRoot");
        if (oldMap != null)
            DestroyImmediate(oldMap);

        var root = new GameObject("MapRoot");

        GenerateGround(root.transform);
        GenerateBeachArea(root.transform);
        GenerateForestArea(root.transform);
        GenerateMountainArea(root.transform);
        GenerateWater(root.transform);
        SetupSpawnAndEscapePoints();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[MapGenerator] Island map generated!");
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

    [MenuItem("Tools/Map Generator/Bake NavMesh")]
    public static void BakeNavMesh()
    {
        // MapRoot의 Ground 하위 모든 오브젝트에 Static 플래그 설정
        var ground = GameObject.Find("MapRoot/Ground");
        if (ground != null)
        {
            foreach (Transform child in ground.transform)
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.NavigationStatic);
            }
        }

        // NavMesh Bake
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[MapGenerator] NavMesh baked!");
    }

    private static void GenerateGround(Transform _parent)
    {
        var groundParent = new GameObject("Ground");
        groundParent.transform.SetParent(_parent);

        // 해안가 영역 (남쪽) — 잔디
        PlaceGroundTiles(groundParent.transform, "SM_Gen_Env_Ground_Grass",
            -HALF_MAP, -HALF_MAP, HALF_MAP, -HALF_MAP + 30f, GROUND_TILE_SIZE);

        // 숲 영역 (중앙) — 잔디
        PlaceGroundTiles(groundParent.transform, "SM_Gen_Env_Ground_Grass",
            -HALF_MAP, -HALF_MAP + 30f, HALF_MAP, HALF_MAP - 30f, GROUND_TILE_SIZE);

        // 산지 영역 (북쪽) — 흙
        PlaceGroundTiles(groundParent.transform, "SM_Gen_Env_Ground_Dirt",
            -HALF_MAP, HALF_MAP - 30f, HALF_MAP, HALF_MAP, GROUND_TILE_SIZE);
    }

    private static void PlaceGroundTiles(Transform _parent, string _prefabPrefix,
        float _minX, float _minZ, float _maxX, float _maxZ, float _tileSize)
    {
        var prefab = FindPrefab($"{_prefabPrefix}_01");
        if (prefab == null)
        {
            // 프리팹 없으면 기본 Plane 생성
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.transform.SetParent(_parent);
            plane.transform.position = new Vector3((_minX + _maxX) / 2f, 0, (_minZ + _maxZ) / 2f);
            plane.transform.localScale = new Vector3((_maxX - _minX) / 10f, 1, (_maxZ - _minZ) / 10f);
            plane.name = $"Ground_{_prefabPrefix}";
            return;
        }

        for (float x = _minX; x < _maxX; x += _tileSize)
        {
            for (float z = _minZ; z < _maxZ; z += _tileSize)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.SetParent(_parent);
                go.transform.position = new Vector3(x + _tileSize / 2f, 0, z + _tileSize / 2f);
            }
        }
    }

    private static void GenerateBeachArea(Transform _parent)
    {
        var area = new GameObject("Area_Beach");
        area.transform.SetParent(_parent);

        // 나무 (참나무) — 해안가에 듬성듬성
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Tree_01", 8,
            -40f, -HALF_MAP, 40f, -HALF_MAP + 25f, 2f);

        // 바위
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_02", 6,
            -40f, -HALF_MAP, 40f, -HALF_MAP + 25f, 1f);

        // 풀
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Grass_01", 15,
            -40f, -HALF_MAP, 40f, -HALF_MAP + 25f, 0.5f);

        // 조개/작은 바위
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_Pebbles_01", 10,
            -40f, -HALF_MAP, 40f, -HALF_MAP + 15f, 0.5f);
    }

    private static void GenerateForestArea(Transform _parent)
    {
        var area = new GameObject("Area_Forest");
        area.transform.SetParent(_parent);

        // 소나무 — 숲 밀집
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Tree_Pine_02", 15,
            -40f, -15f, 40f, 15f, 3f);

        // 일반 나무
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Tree_02", 10,
            -40f, -15f, 40f, 15f, 2.5f);

        // 덤불 (약초 덤불 표현)
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Bush_01", 12,
            -40f, -15f, 40f, 15f, 1f);

        // 큰 덤불
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Bush_Large_01", 6,
            -40f, -15f, 40f, 15f, 1.5f);

        // 꽃
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Flowers_05", 10,
            -30f, -10f, 30f, 10f, 0.5f);

        // 버섯
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Mushroom_01", 6,
            -30f, -10f, 30f, 10f, 0.5f);

        // 언덕
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Hill_04", 2,
            -25f, -10f, 25f, 10f, 8f);
    }

    private static void GenerateMountainArea(Transform _parent)
    {
        var area = new GameObject("Area_Mountain");
        area.transform.SetParent(_parent);

        // 큰 바위
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_07", 8,
            -40f, 20f, 40f, 45f, 3f);

        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_10", 4,
            -40f, 20f, 40f, 45f, 4f);

        // 절벽
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Dirt_Cliff_06", 3,
            -30f, 30f, 30f, 43f, 10f);

        // 산 배경
        var mountainPrefab = FindPrefab("SM_Gen_Env_Mountain_02");
        if (mountainPrefab != null)
        {
            var m1 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPrefab);
            m1.transform.SetParent(area.transform);
            m1.transform.position = new Vector3(-20f, 0, 48f);
            m1.transform.localScale = Vector3.one * 2f;

            var m2 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPrefab);
            m2.transform.SetParent(area.transform);
            m2.transform.position = new Vector3(20f, 0, 48f);
            m2.transform.localScale = Vector3.one * 1.5f;
        }

        // 죽은 나무
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Tree_Dead_01", 4,
            -35f, 20f, 35f, 43f, 2f);

        // 철광석 바위
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_05", 4,
            -30f, 25f, 30f, 40f, 3f);
    }

    private static void GenerateWater(Transform _parent)
    {
        var waterPrefab = FindPrefab("SM_Gen_Env_Water_Dip_01");
        if (waterPrefab == null) return;

        var waterParent = new GameObject("Water");
        waterParent.transform.SetParent(_parent);

        // 해안가 주변 물
        for (float x = -HALF_MAP; x < HALF_MAP; x += 15f)
        {
            var water = (GameObject)PrefabUtility.InstantiatePrefab(waterPrefab);
            water.transform.SetParent(waterParent.transform);
            water.transform.position = new Vector3(x, -0.5f, -HALF_MAP - 10f);
            water.transform.localScale = Vector3.one * 3f;
        }
    }

    private static void SetupSpawnAndEscapePoints()
    {
        // 스폰 지점 이동 (해안가 남쪽)
        MoveObject("StartPosition1", new Vector3(-3f, 0, -42f));
        MoveObject("StartPosition2", new Vector3(3f, 0, -42f));
        MoveObject("StartPosition3", new Vector3(-6f, 0, -38f));
        MoveObject("StartPosition4", new Vector3(6f, 0, -38f));

        // 탈출 지점 이동 (산지 북쪽)
        MoveObject("EscapePoint", new Vector3(0, 0, 45f));

        // 몬스터 스폰 영역 이동
        MoveObject("Area1_Beach", new Vector3(0, 0, -35f));
        MoveObject("Area2_Forest", new Vector3(0, 0, 0));
        MoveObject("Area3_Mountain", new Vector3(0, 0, 30f));
    }

    private static void MoveObject(string _name, Vector3 _position)
    {
        var go = GameObject.Find(_name);
        if (go != null)
            go.transform.position = _position;
    }

    private static void PlaceRandomObjects(Transform _parent, string _prefabName, int _count,
        float _minX, float _minZ, float _maxX, float _maxZ, float _scaleVariance)
    {
        var prefab = FindPrefab(_prefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"[MapGenerator] Prefab not found: {_prefabName}");
            return;
        }

        for (int i = 0; i < _count; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(_parent);

            float x = Random.Range(_minX, _maxX);
            float z = Random.Range(_minZ, _maxZ);
            float y = GetTerrainHeight(x, z);
            go.transform.position = new Vector3(x, y, z);

            float scale = Random.Range(1f, 1f + _scaleVariance);
            go.transform.localScale = Vector3.one * scale;

            float rotation = Random.Range(0f, 360f);
            go.transform.rotation = Quaternion.Euler(0, rotation, 0);
        }
    }

    private static float GetTerrainHeight(float _x, float _z)
    {
        // 위에서 아래로 레이캐스트하여 지형 표면 높이를 구함
        Ray ray = new Ray(new Vector3(_x, 200f, _z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 400f))
        {
            return hit.point.y;
        }
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
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
        return null;
    }

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

    private static GameObject GetRandomPrefab(PrefabCategory _category)
    {
        var pool = FindPrefabsByCategory(_category);
        if (pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }
}
#endif
