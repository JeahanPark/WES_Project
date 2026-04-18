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
    private const float MAP_SIZE = 200f;
    private const float HALF_MAP = MAP_SIZE / 2f;

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

        // 해안가 영역 (남쪽) — 밝은 모래/잔디
        PlaceGroundTiles(groundParent.transform, "SM_Gen_Env_Ground_Grass",
            -HALF_MAP, -HALF_MAP, HALF_MAP, -HALF_MAP + 60f, 20f);

        // 숲 영역 (중앙) — 잔디
        PlaceGroundTiles(groundParent.transform, "SM_Gen_Env_Ground_Grass",
            -HALF_MAP, -HALF_MAP + 60f, HALF_MAP, HALF_MAP - 60f, 20f);

        // 산지 영역 (북쪽) — 흙
        PlaceGroundTiles(groundParent.transform, "SM_Gen_Env_Ground_Dirt",
            -HALF_MAP, HALF_MAP - 60f, HALF_MAP, HALF_MAP, 20f);
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
            -80f, -HALF_MAP, 80f, -HALF_MAP + 50f, 2f);

        // 바위
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_02", 6,
            -80f, -HALF_MAP, 80f, -HALF_MAP + 50f, 1f);

        // 풀
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Grass_01", 15,
            -80f, -HALF_MAP, 80f, -HALF_MAP + 50f, 0.5f);

        // 조개/작은 바위
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_Pebbles_01", 10,
            -80f, -HALF_MAP, 80f, -HALF_MAP + 30f, 0.5f);
    }

    private static void GenerateForestArea(Transform _parent)
    {
        var area = new GameObject("Area_Forest");
        area.transform.SetParent(_parent);

        // 소나무 — 숲 밀집
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Tree_Pine_02", 20,
            -80f, -30f, 80f, 30f, 3f);

        // 일반 나무
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Tree_02", 12,
            -80f, -30f, 80f, 30f, 2.5f);

        // 덤불 (약초 덤불 표현)
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Bush_01", 15,
            -80f, -30f, 80f, 30f, 1f);

        // 큰 덤불
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Bush_Large_01", 8,
            -80f, -30f, 80f, 30f, 1.5f);

        // 꽃
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Flowers_05", 12,
            -60f, -20f, 60f, 20f, 0.5f);

        // 버섯
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Mushroom_01", 8,
            -60f, -20f, 60f, 20f, 0.5f);

        // 언덕
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Hill_04", 3,
            -50f, -20f, 50f, 20f, 8f);
    }

    private static void GenerateMountainArea(Transform _parent)
    {
        var area = new GameObject("Area_Mountain");
        area.transform.SetParent(_parent);

        // 큰 바위
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_07", 10,
            -80f, 40f, 80f, 90f, 3f);

        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_10", 6,
            -80f, 40f, 80f, 90f, 4f);

        // 절벽
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Dirt_Cliff_06", 4,
            -60f, 60f, 60f, 85f, 10f);

        // 산 배경
        var mountainPrefab = FindPrefab("SM_Gen_Env_Mountain_02");
        if (mountainPrefab != null)
        {
            var m1 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPrefab);
            m1.transform.SetParent(area.transform);
            m1.transform.position = new Vector3(-40f, 0, 95f);
            m1.transform.localScale = Vector3.one * 2f;

            var m2 = (GameObject)PrefabUtility.InstantiatePrefab(mountainPrefab);
            m2.transform.SetParent(area.transform);
            m2.transform.position = new Vector3(40f, 0, 95f);
            m2.transform.localScale = Vector3.one * 1.5f;
        }

        // 죽은 나무
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Tree_Dead_01", 5,
            -70f, 40f, 70f, 85f, 2f);

        // 철광석 바위 (밝은 색 바위로 구분)
        PlaceRandomObjects(area.transform, "SM_Gen_Env_Rock_05", 5,
            -60f, 50f, 60f, 80f, 3f);
    }

    private static void GenerateWater(Transform _parent)
    {
        var waterPrefab = FindPrefab("SM_Gen_Env_Water_Dip_01");
        if (waterPrefab == null) return;

        var waterParent = new GameObject("Water");
        waterParent.transform.SetParent(_parent);

        // 해안가 주변 물
        for (float x = -HALF_MAP; x < HALF_MAP; x += 30f)
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
        MoveObject("StartPosition1", new Vector3(-5f, 0, -85f));
        MoveObject("StartPosition2", new Vector3(5f, 0, -85f));
        MoveObject("StartPosition3", new Vector3(-10f, 0, -80f));
        MoveObject("StartPosition4", new Vector3(10f, 0, -80f));

        // 탈출 지점 이동 (산지 북쪽)
        MoveObject("EscapePoint", new Vector3(0, 0, 90f));
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
            go.transform.position = new Vector3(x, 0, z);

            float scale = Random.Range(1f, 1f + _scaleVariance);
            go.transform.localScale = Vector3.one * scale;

            float rotation = Random.Range(0f, 360f);
            go.transform.rotation = Quaternion.Euler(0, rotation, 0);
        }
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
}
#endif
