#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R2 출시 6지역 — 지형 지역 시각 구분(designer 슬라이스2b-2).
/// MapGenerator가 생성한 GeneratedMap의 Ground/Slopes/Hills 타일을 종단 Z로 밴드 판정해
/// 밴드별 틴트 머티리얼 인스턴스로 교체한다. "완벽한 아트"가 아니라 "지역이 구분되는" 수준이 목표.
///
/// 자산 우선순위 트리: 1단계(GameResource 재사용)·2단계(Synty 차용) 메쉬는 그대로 두고
/// 3단계(Procedural 머티리얼 변형 — 표준 URP Lit _BaseColor 틴트)로 지역 구분만 부여.
///
/// 밴드 경계 = WorldAreaInfo.csv AxisMin/AxisMax 단일 진실원(MapGenerator와 동일 소스).
/// 틴트 머티리얼은 Assets/GameResource/Material/Region/ 에 (원본명_지역명) 으로 캐시 생성.
/// </summary>
public static class Release6RegionTerrainSetup
{
    private const string AREA_CSV_PATH = "Assets/CSVInfo/WorldAreaInfo.csv";
    private const string TINT_DIR = "Assets/GameResource/Material/Region";

    // 밴드 인덱스(d=0..5) → 지형 틴트 색(곱연산). 톤 가이드 §7: 앞→뒤로 채도·온기 하강.
    // d0 해안: 따뜻한 모래빛 / d1 숲: 자연 녹 / d2 늪: 탁한 녹갈 / d3 산지: 회갈 바위
    // d4 설원: 창백 흰눈 / d5 폐허: 잿빛
    private static readonly Color[] REGION_TINT =
    {
        new Color(1.00f, 0.92f, 0.72f), // d0 해안 — 따뜻한 모래(노란기)
        new Color(0.78f, 0.95f, 0.70f), // d1 숲   — 선명한 녹
        new Color(0.50f, 0.60f, 0.36f), // d2 늪지 — 탁한 녹갈
        new Color(0.52f, 0.43f, 0.32f), // d3 산지 — 진한 흙갈/바위
        new Color(1.00f, 1.00f, 1.00f), // d4 설원 — 최대 흰(어두운 조명서도 가장 밝게)
        new Color(0.44f, 0.44f, 0.48f), // d5 폐허 — 어두운 청회(잿빛)
    };

    private static readonly string[] REGION_SUFFIX =
        { "Beach", "Forest", "Swamp", "Mountain", "Snow", "Ruins" };

    private struct Band { public float ZMin; public float ZMax; }

    [MenuItem("WES/Release6Region/Apply Terrain Region Tints")]
    public static void ApplyTints()
    {
        var bands = LoadBands();
        if (bands == null || bands.Count == 0)
        {
            Debug.LogError("[Release6Region] WorldAreaInfo.csv 밴드 파싱 실패");
            return;
        }

        var generated = GameObject.Find("MapRoot/GeneratedMap");
        if (generated == null)
        {
            Debug.LogError("[Release6Region] MapRoot/GeneratedMap 없음. Tools/Map Generator/Generate Island Map 먼저 실행");
            return;
        }

        if (!AssetDatabase.IsValidFolder(TINT_DIR))
        {
            Directory.CreateDirectory(TINT_DIR);
            AssetDatabase.Refresh();
        }

        // (원본 머티리얼 인스턴스ID, band) → 틴트 머티리얼 캐시
        var cache = new Dictionary<(int, int), Material>();
        int tiledRenderers = 0;

        string[] groups = { "Ground", "Slopes", "Hills" };
        foreach (var grp in groups)
        {
            var root = generated.transform.Find(grp);
            if (root == null) continue;

            foreach (Transform tile in root)
            {
                int band = GetBandIndex(tile.position.z, bands);
                if (band < 0) band = 0;
                if (band >= REGION_TINT.Length) band = REGION_TINT.Length - 1;

                var renderers = tile.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    var shared = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < shared.Length; i++)
                    {
                        var src = shared[i];
                        if (src == null) continue;
                        var tint = GetOrCreateTint(src, band, cache);
                        if (tint != null && tint != src) { shared[i] = tint; changed = true; }
                    }
                    if (changed) r.sharedMaterials = shared;
                    tiledRenderers++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Release6Region] 지역 틴트 적용 완료 — 그룹 {groups.Length}, 처리 렌더러 {tiledRenderers}, 생성 머티리얼 {cache.Count}");
    }

    private static Material GetOrCreateTint(Material _src, int _band, Dictionary<(int, int), Material> _cache)
    {
        if (!_src.HasProperty("_BaseColor") && !_src.HasProperty("_Color"))
            return _src; // 틴트 불가 셰이더는 원본 유지

        var key = (_src.GetInstanceID(), _band);
        if (_cache.TryGetValue(key, out var existing)) return existing;

        string suffix = _band >= 0 && _band < REGION_SUFFIX.Length ? REGION_SUFFIX[_band] : _band.ToString();
        string assetPath = $"{TINT_DIR}/{_src.name}_{suffix}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            mat = new Material(_src);
            AssetDatabase.CreateAsset(mat, assetPath);
        }

        Color tint = REGION_TINT[Mathf.Clamp(_band, 0, REGION_TINT.Length - 1)];
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
        EditorUtility.SetDirty(mat);

        _cache[key] = mat;
        return mat;
    }

    // ===== CSV 밴드 파싱 (MapGenerator와 동일 규칙) =====
    private static List<Band> LoadBands()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AREA_CSV_PATH);
        string text = asset != null ? asset.text : (File.Exists(AREA_CSV_PATH) ? File.ReadAllText(AREA_CSV_PATH) : null);
        if (string.IsNullOrEmpty(text)) return null;

        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        if (lines.Length < 2) return null;

        string[] header = lines[0].Split(',');
        int idxMin = FindCol(header, "AxisMin");
        int idxMax = FindCol(header, "AxisMax");
        if (idxMin < 0 || idxMax < 0) return null;

        var list = new List<Band>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cols = lines[i].Split(',');
            if (cols.Length <= Mathf.Max(idxMin, idxMax)) continue;
            list.Add(new Band { ZMin = ParseF(cols[idxMin]), ZMax = ParseF(cols[idxMax]) });
        }
        list.Sort((a, b) => a.ZMin.CompareTo(b.ZMin));
        return list;
    }

    private static int FindCol(string[] _header, string _name)
    {
        for (int i = 0; i < _header.Length; i++)
        {
            string h = _header[i].Trim();
            int dot = h.IndexOf('.');
            if (dot >= 0) h = h.Substring(0, dot);
            if (h == _name) return i;
        }
        return -1;
    }

    private static float ParseF(string _s) =>
        float.TryParse(_s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

    private static int GetBandIndex(float _z, List<Band> _bands)
    {
        for (int i = 0; i < _bands.Count; i++)
            if (_z >= _bands[i].ZMin && _z < _bands[i].ZMax) return i;
        if (_bands.Count > 0 && _z < _bands[0].ZMin) return 0;
        return _bands.Count - 1;
    }
}
#endif
