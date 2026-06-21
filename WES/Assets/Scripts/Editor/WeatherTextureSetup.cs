using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// R4 ① 날씨 오버레이 Procedural 텍스처 생성기 (비/안개/눈보라).
/// CoreTensionTextureSetup 패턴 계승 — alpha 그라데이션·단색 제어를 코드로 정밀하게 박는다
/// (풀스크린 오버레이는 AI 텍스처보다 procedural이 깔끔·정확. 자산 우선순위 트리 3단계).
///
/// 기획 R4_감각 §5-1 / §7-1:
///  - 비:   화면 가장자리 빗물 얼룩(가장자리 어둑·차가운 회청, 중앙 투명) — 축축·무겁게
///  - 안개: 풀스크린 옅은 회백 베일(I-5 앰비언트 안개 위 추가 강화) — 무엇이 숨었는지 모르는 불안
///  - 눈보라: 백색 흩날림 차폐(중앙도 약하게 깔리는 백색, 가장자리 더 짙음) — 시야가 지워지는 혹독함
///    (실제 차폐 상한은 코드 m_OverlayVisibilityCeiling 0.6이 알파를 클램프 → 텍스처는 풀화이트라도 무방)
///
/// 출력: Assets/GameResource/UI/CoreTension/rain_edge.png / fog_veil.png / snowstorm_white.png
/// 와이어링은 WeatherOverlayBuildSetup(designer 영역, 슬롯 m_RainEdge/m_FogVeil/m_SnowstormOverlay).
/// </summary>
public static class WeatherTextureSetup
{
    private const string DIR = "Assets/GameResource/UI/CoreTension";
    private const int RES = 512; // 풀스크린 오버레이용(GPU 업스케일). 비네팅/노이즈라 저해상도 충분.

    // wes-palette 정렬: 비=차가운 회청, 안개=저채도 회백, 눈보라=백색.
    static readonly Color RainTone = Hex("2b3946"); // 비 가장자리 얼룩(어둑한 청회)
    static readonly Color FogTone  = Hex("c2cbd2"); // 안개 회백 베일
    static readonly Color SnowTone = Hex("f2f5f8"); // 눈보라 백색(살짝 청기)

    [MenuItem("WES/AI Texture/Generate Weather Procedural Sources")]
    public static void Generate()
    {
        EnsureDir(DIR);

        // 비: 가장자리 빗물 얼룩 — 중앙 완전 투명, 가장자리만 어둑하게(시야 보존). 약한 수직 줄무늬로 빗물 질감.
        WriteTex("rain_edge", RainEdge(RainTone, edgeAlpha: 0.75f, innerRadius: 0.42f, power: 1.7f));

        // 안개: 풀스크린 회백 베일 — 중앙도 깔리되(불안) 가장자리로 약간 짙게. 부드러운 노이즈로 균질하지 않게.
        WriteTex("fog_veil", FogVeil(FogTone, centerAlpha: 0.55f, edgeAlpha: 0.85f));

        // 눈보라: 백색 흩날림 차폐 — 전면 백색(중앙 floor 높음), 작은 점 노이즈로 흩날리는 눈 질감. 코드가 최종 알파 클램프.
        WriteTex("snowstorm_white", Snowstorm(SnowTone, centerFloor: 0.70f, edgeAlpha: 1.0f));

        AssetDatabase.Refresh();
        SetSprites();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Weather] 날씨 오버레이 3종 Procedural 텍스처 생성 완료 ({DIR}). 와이어링은 WeatherOverlayBuildSetup.");
    }

    // ── 텍스처 생성기 ──────────────────────────────────────────────

    /// <summary>비: 중앙 투명 → 가장자리 어둑(차가운 청회). 약한 수직 줄무늬(빗물 흐름) 가미.</summary>
    static Color[] RainEdge(Color col, float edgeAlpha, float innerRadius, float power)
    {
        var px = new Color[RES * RES];
        Vector2 c = new Vector2(0.5f, 0.5f);
        float maxD = 0.5f * Mathf.Sqrt(2f);
        for (int y = 0; y < RES; y++)
        for (int x = 0; x < RES; x++)
        {
            Vector2 uv = new Vector2((x + 0.5f) / RES, (y + 0.5f) / RES);
            float d = Vector2.Distance(uv, c) / maxD;
            float t = Mathf.Pow(Mathf.Clamp01(Mathf.InverseLerp(innerRadius, 1f, d)), power);
            // 수직 줄무늬(빗물): 가장자리 영역에서만 약하게 알파 변조
            float streak = 0.85f + 0.15f * Mathf.Sin(uv.x * Mathf.PI * 2f * 18f);
            float a = edgeAlpha * t * streak;
            px[y * RES + x] = new Color(col.r, col.g, col.b, Mathf.Clamp01(a));
        }
        return px;
    }

    /// <summary>안개: 풀스크린 회백 베일. 중앙도 깔리고(centerAlpha) 가장자리로 약간 짙게(edgeAlpha). 부드러운 노이즈로 불균질.</summary>
    static Color[] FogVeil(Color col, float centerAlpha, float edgeAlpha)
    {
        var px = new Color[RES * RES];
        Vector2 c = new Vector2(0.5f, 0.5f);
        float maxD = 0.5f * Mathf.Sqrt(2f);
        for (int y = 0; y < RES; y++)
        for (int x = 0; x < RES; x++)
        {
            Vector2 uv = new Vector2((x + 0.5f) / RES, (y + 0.5f) / RES);
            float d = Vector2.Distance(uv, c) / maxD;
            float baseA = Mathf.Lerp(centerAlpha, edgeAlpha, Mathf.SmoothStep(0f, 1f, d));
            // 저주파 안개 노이즈(±0.12)로 균질하지 않게
            float n = Mathf.PerlinNoise(uv.x * 3.5f, uv.y * 3.5f);
            float a = baseA + (n - 0.5f) * 0.24f;
            px[y * RES + x] = new Color(col.r, col.g, col.b, Mathf.Clamp01(a));
        }
        return px;
    }

    /// <summary>눈보라: 전면 백색(중앙 floor 높음) + 작은 점 노이즈(흩날리는 눈). 가장자리(성에) 더 짙음. 최종 알파는 코드 천장이 클램프.</summary>
    static Color[] Snowstorm(Color col, float centerFloor, float edgeAlpha)
    {
        var px = new Color[RES * RES];
        Vector2 c = new Vector2(0.5f, 0.5f);
        float maxD = 0.5f * Mathf.Sqrt(2f);
        for (int y = 0; y < RES; y++)
        for (int x = 0; x < RES; x++)
        {
            Vector2 uv = new Vector2((x + 0.5f) / RES, (y + 0.5f) / RES);
            float d = Vector2.Distance(uv, c) / maxD;
            float baseA = Mathf.Lerp(centerFloor, edgeAlpha, Mathf.SmoothStep(0f, 1f, d));
            // 흩날리는 눈: 고주파 점 노이즈로 작은 알파 밝기 변동(눈송이 산란 느낌)
            float fn = Mathf.PerlinNoise(uv.x * 40f, uv.y * 40f);
            float flake = 0.88f + 0.12f * fn;
            float a = baseA * flake;
            px[y * RES + x] = new Color(col.r, col.g, col.b, Mathf.Clamp01(a));
        }
        return px;
    }

    // ── 유틸 (CoreTensionTextureSetup과 동일) ──────────────────────

    static void WriteTex(string name, Color[] px)
    {
        var tex = new Texture2D(RES, RES, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(Path.GetFullPath($"{DIR}/{name}.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void SetSprites()
    {
        foreach (var name in new[] { "rain_edge", "fog_veil", "snowstorm_white" })
        {
            string path = $"{DIR}/{name}.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
    }

    static void EnsureDir(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
