using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 코어텐션(추위/낮밤/비네팅/사망/앰비언트) Procedural 텍스처·머티리얼 소스 생성기.
/// 리소스리스트 G-2/G-3/G-13/G-14, I-5 — Procedural + 코드영역(client) 분류.
/// 디자이너는 정확한 팔레트(wes-palette.md hex)로 단색 틴트·방사형 비네팅·그라데이션
/// 텍스처와 머티리얼 슬롯을 제공하고, 단계 전환·펄스·포스트프로세스 합성은 client가 와이어링.
/// AI(Gemini) 대신 코드로 생성하는 이유: alpha 그라데이션·단색 제어를 정밀하게 박아야
/// 팔레트 hex가 정확히 반영되고 풀스크린 오버레이가 깔끔하기 때문.
///
/// 출력: Assets/GameResource/UI/CoreTension/*.png (+ Overlay unlit material 1종 템플릿)
/// </summary>
public static class CoreTensionTextureSetup
{
    private const string DIR = "Assets/GameResource/UI/CoreTension";
    private const int RES = 512; // 풀스크린 오버레이용. 비네팅/그라데이션이라 저해상도로 충분(GPU 업스케일).

    // wes-palette.md
    static readonly Color ColdTint1 = Hex("3a5a73");
    static readonly Color ColdTint2 = Hex("243f5c");
    static readonly Color ColdTint3 = Hex("cfe2ec");
    static readonly Color VignetteRed = Hex("8a1414");
    static readonly Color Carbon = Hex("1c1a17");
    // 낮밤 4구간
    static readonly Color DayTone   = Hex("7d8a96"); // 낮 청회
    static readonly Color DuskTone  = Hex("9a6a52"); // 황혼 주황보라 기운
    static readonly Color NightTone = Hex("1e2740"); // 밤 남색
    static readonly Color DawnTone  = Hex("8fa6b8"); // 새벽 차가운 안도

    [MenuItem("WES/AI Texture/Generate CoreTension Procedural Sources")]
    public static void Generate()
    {
        EnsureDir(DIR);

        // G-2 추위 오버레이 3단계: 가장자리 성에 비네팅(중앙 투명→가장자리 짙음).
        // 기획 §5.2/§8.1: 1·2단계는 "중앙 시야 깨끗"(centerAlpha=0/낮음), 3단계만 "전체 서리".
        // centerAlpha = 중앙 알파(투명여백), edgeAlpha = 코너 최대 알파. 코드 캡 0.85가 위에 곱해짐.
        WriteTex("cold_overlay_1", EdgeVignetteTint(ColdTint1, centerAlpha: 0.00f, edgeAlpha: 0.22f, innerRadius: 0.45f, power: 1.8f));
        WriteTex("cold_overlay_2", EdgeVignetteTint(ColdTint2, centerAlpha: 0.05f, edgeAlpha: 0.45f, innerRadius: 0.32f, power: 1.6f));
        WriteTex("cold_overlay_3", EdgeVignetteTint(ColdTint3, centerAlpha: 0.28f, edgeAlpha: 0.85f, innerRadius: 0.18f, power: 1.4f));

        // G-13 저체력 적색 비네팅(중앙 투명 → 가장자리 적색, 전투 채널)
        WriteTex("vignette_red", RadialVignette(VignetteRed, maxAlpha: 0.55f, innerRadius: 0.40f, power: 2.2f));

        // G-14 사망 암전 비네팅(가장자리부터 carbon 암전, 중앙도 약하게 깔림)
        WriteTex("death_overlay", RadialVignette(Carbon, maxAlpha: 0.92f, innerRadius: 0.20f, power: 1.6f, centerFloor: 0.25f));

        // G-3 낮밤 톤: 세로 1px×4구간 그라데이션 LUT(낮→황혼→밤→새벽). client가 time→U좌표 샘플.
        WriteTex("daynight_gradient", DayNightGradient());

        // I-5 앰비언트 안개: 부드러운 수평 노이즈 그라데이션(저채도 회청, 약한 알파). 스크롤용 타일.
        WriteTex("ambient_fog", FogNoise(Hex("8a97a3"), maxAlpha: 0.22f));

        // G-3 낮밤 틴트 레이어용 흰색 단색(코드가 color로 틴트색 곱). 그라데이션 LUT 이중적용 회피 — 명세 5절 권장.
        WriteTex("daynight_tint_white", SolidWhite());

        AssetDatabase.Refresh();
        SetOverlaySprites();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CoreTension] 7종 Procedural 텍스처 생성 완료 ({DIR}). 와이어링은 client 영역.");
    }

    // ── 텍스처 생성기들 ──────────────────────────────────────────

    /// <summary>전체 흰색 불투명(낮밤 틴트 레이어 베이스). 코드가 color로 곱.</summary>
    static Color[] SolidWhite()
    {
        var px = new Color[RES * RES];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        return px;
    }

    /// <summary>중앙(centerAlpha)에서 가장자리(edgeAlpha)로 짙어지는 단색 비네트 틴트(추위 오버레이).
    /// innerRadius 안쪽은 centerAlpha 평탄, 그 밖부터 코너까지 power 곡선으로 edgeAlpha까지 상승.
    /// centerAlpha=0이면 중앙 완전 투명(시야 보존).</summary>
    static Color[] EdgeVignetteTint(Color tint, float centerAlpha, float edgeAlpha, float innerRadius, float power)
    {
        var px = new Color[RES * RES];
        Vector2 c = new Vector2(0.5f, 0.5f);
        float maxD = 0.5f * Mathf.Sqrt(2f);
        for (int y = 0; y < RES; y++)
        for (int x = 0; x < RES; x++)
        {
            Vector2 uv = new Vector2((x + 0.5f) / RES, (y + 0.5f) / RES);
            float d = Vector2.Distance(uv, c) / maxD; // 0 중앙 ~ 1 코너
            float t = Mathf.Pow(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, 1f, d)), power);
            float a = Mathf.Lerp(centerAlpha, edgeAlpha, t);
            px[y * RES + x] = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(a));
        }
        return px;
    }

    /// <summary>중앙 투명 → 가장자리 단색(비네팅). centerFloor>0이면 중앙도 약하게 깔림.</summary>
    static Color[] RadialVignette(Color col, float maxAlpha, float innerRadius, float power, float centerFloor = 0f)
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
            float a = Mathf.Lerp(centerFloor, 1f, t) * maxAlpha;
            px[y * RES + x] = new Color(col.r, col.g, col.b, Mathf.Clamp01(a));
        }
        return px;
    }

    /// <summary>가로 4구간 LUT 그라데이션(낮→황혼→밤→새벽). 불투명. client가 U=시간으로 샘플.</summary>
    static Color[] DayNightGradient()
    {
        var px = new Color[RES * RES];
        Color[] stops = { DayTone, DuskTone, NightTone, DawnTone, DayTone }; // 순환되게 끝=시작
        int seg = stops.Length - 1;
        for (int x = 0; x < RES; x++)
        {
            float u = (float)x / (RES - 1);
            float fseg = u * seg;
            int i = Mathf.Clamp(Mathf.FloorToInt(fseg), 0, seg - 1);
            float f = fseg - i;
            Color col = Color.Lerp(stops[i], stops[i + 1], Mathf.SmoothStep(0f, 1f, f));
            col.a = 1f;
            for (int y = 0; y < RES; y++) px[y * RES + x] = col;
        }
        return px;
    }

    /// <summary>부드러운 안개 노이즈(저채도 회청, 수평 스크롤 타일용). 가로 seamless.</summary>
    static Color[] FogNoise(Color col, float maxAlpha)
    {
        var px = new Color[RES * RES];
        for (int y = 0; y < RES; y++)
        for (int x = 0; x < RES; x++)
        {
            // 가로 seamless: 각도 기반 노이즈 + 저주파
            float ang = (float)x / RES * Mathf.PI * 2f;
            float nx = Mathf.Cos(ang) * 1.5f + 4f;
            float ny = Mathf.Sin(ang) * 1.5f + 4f;
            float n1 = Mathf.PerlinNoise(nx, (float)y / RES * 2.5f);
            float n2 = Mathf.PerlinNoise(nx * 2.3f + 10f, (float)y / RES * 5f + 10f);
            float n = Mathf.Clamp01(n1 * 0.7f + n2 * 0.3f);
            // 위로 갈수록 옅게(하늘 안개 느낌)
            float vfade = Mathf.Lerp(0.4f, 1f, (float)y / RES);
            float a = n * maxAlpha * vfade;
            px[y * RES + x] = new Color(col.r, col.g, col.b, Mathf.Clamp01(a));
        }
        return px;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static void WriteTex(string name, Color[] px)
    {
        var tex = new Texture2D(RES, RES, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(Path.GetFullPath($"{DIR}/{name}.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>생성한 오버레이/그라데이션을 Sprite로 import(클램프, 알파유지).</summary>
    static void SetOverlaySprites()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DIR });
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            // 그라데이션 LUT는 wrap clamp, 안개는 가로 repeat
            ti.wrapMode = path.Contains("ambient_fog") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
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
