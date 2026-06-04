using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// AI(Gemini)로 생성·다운로드한 이미지를 GameResource로 들여오고
/// 카테고리 Sprite Atlas에 편입하는 디자이너용 Editor 유틸.
/// - 외부 다운로드 PNG → 지정 카테고리 폴더로 복사
/// - TextureImporter를 Sprite(2D UI 표준)로 설정
/// - 카테고리 폴더를 Sprite Atlas의 packables에 등록(폴더 단위 자동 편입)
/// 톤·실제 생성은 designer 에이전트가 수행. 본 유틸은 import 표준화만 담당.
/// </summary>
public static class AiTextureImportSetup
{
    public const string ATLAS_DIR = "Assets/GameResource/UI/Atlas";

    /// <summary>
    /// 다운로드 PNG를 카테고리 폴더로 복사·import하고 아틀라스에 편입한다.
    /// </summary>
    /// <param name="srcPng">다운로드된 원본 PNG 절대경로</param>
    /// <param name="categoryDir">대상 폴더 (예: "Assets/GameResource/Image/ItemIcon")</param>
    /// <param name="assetName">확장자 제외 자산명 (예: "campfire_icon")</param>
    /// <param name="atlasName">카테고리 아틀라스명 (예: "Icons"), 비우면 아틀라스 생략</param>
    /// <returns>생성된 에셋 경로</returns>
    public static string ImportAndPack(string srcPng, string categoryDir, string assetName, string atlasName)
    {
        EnsureDir(categoryDir);
        string destPath = $"{categoryDir}/{assetName}.png";
        File.Copy(srcPng, Path.GetFullPath(destPath), overwrite: true);
        AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(destPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();

        if (!string.IsNullOrEmpty(atlasName))
            PackFolderIntoAtlas(categoryDir, atlasName);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AiTexture] imported {destPath} (atlas={atlasName})");
        return destPath;
    }

    /// <summary>카테고리 폴더를 아틀라스 packables에 등록(중복 시 무시).</summary>
    public static void PackFolderIntoAtlas(string categoryDir, string atlasName)
    {
        EnsureDir(ATLAS_DIR);
        string atlasPath = $"{ATLAS_DIR}/{atlasName}.spriteatlas";
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }

        var folder = AssetDatabase.LoadAssetAtPath<Object>(categoryDir);
        if (folder == null) return;

        foreach (var packed in atlas.GetPackables())
            if (packed == folder) return; // 이미 등록됨

        SpriteAtlasExtensions.Add(atlas, new Object[] { folder });
        EditorUtility.SetDirty(atlas);
    }

    /// <summary>
    /// 폴더 내 모든 텍스처를 Sprite(single)로 정규화한다.
    /// Unity 기본 import가 Default(0)/Sprite(8)를 비결정적으로 정하는 문제를 방지 —
    /// AI 생성 이미지를 폴더에 떨군 뒤 이 메서드로 일괄 보정한다.
    /// </summary>
    /// <returns>Sprite로 바꾼 텍스처 수</returns>
    public static int NormalizeFolderToSprite(string categoryDir)
    {
        int fixedCount = 0;
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { categoryDir });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            if (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single)
                continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
            fixedCount++;
        }
        return fixedCount;
    }

    private static void EnsureDir(string dir)
    {
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
            string leaf = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    // ── ItemIcon 폴더 Sprite 정규화 + Icons 아틀라스 편입 (AI 생성 후 일괄 처리) ──
    [MenuItem("WES/AI Texture/Normalize ItemIcon Sprites And Pack Icons")]
    public static void NormalizeAndPackItemIcons()
    {
        const string dir = "Assets/GameResource/Image/ItemIcon";
        int fixedCount = NormalizeFolderToSprite(dir);
        PackFolderIntoAtlas(dir, "Icons");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AiTexture] ItemIcon 정규화 {fixedCount}건 → Sprite, Icons.spriteatlas 편입 완료");
    }

    /// <summary>
    /// PNG를 가장자리 inset 비율만큼 잘라 어두운 액자 프레임 + 우하단 Gemini 워터마크를 제거하고 destPng로 저장.
    /// 멱등성을 위해 항상 .playwright-output의 **원본**에서 크롭해 GameResource로 저장할 것(크롭본 재크롭 금지).
    /// </summary>
    /// <param name="srcPng">원본 PNG 절대경로(미크롭)</param>
    /// <param name="destPng">저장 대상 절대경로</param>
    /// <param name="inset">가장자리 잘라낼 비율(0~0.45). 기본 0.08 = 상하좌우 8%</param>
    public static void CropInsetPng(string srcPng, string destPng, float inset = 0.08f)
    {
        inset = Mathf.Clamp(inset, 0f, 0.45f);
        byte[] data = File.ReadAllBytes(srcPng);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data); // LoadImage로 만든 텍스처는 CPU 읽기 가능
        int w = tex.width, h = tex.height;
        int mx = Mathf.RoundToInt(w * inset);
        int my = Mathf.RoundToInt(h * inset);
        int cw = Mathf.Max(1, w - mx * 2);
        int ch = Mathf.Max(1, h - my * 2);
        var px = tex.GetPixels(mx, my, cw, ch);
        var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        outTex.SetPixels(px);
        outTex.Apply();
        File.WriteAllBytes(destPng, outTex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(outTex);
    }

    /// <summary>
    /// PNG의 상/하 여백만 비율로 잘라낸다(좌우 폭은 유지). 가로 게이지 fill 스트립처럼
    /// 위아래 검은 배경 여백을 제거해 스트립만 남길 때 사용. destPng로 저장.
    /// </summary>
    /// <param name="srcPng">원본 PNG 절대경로</param>
    /// <param name="destPng">저장 대상 절대경로</param>
    /// <param name="vInset">상/하 각각 잘라낼 비율(0~0.45)</param>
    public static void CropVerticalInsetPng(string srcPng, string destPng, float vInset = 0.30f)
    {
        vInset = Mathf.Clamp(vInset, 0f, 0.45f);
        byte[] data = File.ReadAllBytes(srcPng);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data);
        int w = tex.width, h = tex.height;
        int my = Mathf.RoundToInt(h * vInset);
        int ch = Mathf.Max(1, h - my * 2);
        var px = tex.GetPixels(0, my, w, ch);
        var outTex = new Texture2D(w, ch, TextureFormat.RGBA32, false);
        outTex.SetPixels(px);
        outTex.Apply();
        File.WriteAllBytes(destPng, outTex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(outTex);
    }

    /// <summary>
    /// 우하단 Gemini 워터마크만 불투명 배경색으로 덮어 제거하고 destPng로 저장.
    /// 9-slice 프레임은 전방위 inset 크롭 시 모서리(못·밧줄 매듭) 디테일이 깎이므로,
    /// 워터마크가 있는 우하단 코너 영역만 인근 배경색으로 칠해 가린다.
    /// </summary>
    /// <param name="srcPng">원본 PNG 절대경로</param>
    /// <param name="destPng">저장 대상 절대경로</param>
    /// <param name="wRatio">덮을 영역 가로 비율(우측 기준). 기본 0.22</param>
    /// <param name="hRatio">덮을 영역 세로 비율(하단 기준). 기본 0.10</param>
    public static void MaskCornerWatermarkPng(string srcPng, string destPng, float wRatio = 0.22f, float hRatio = 0.10f)
    {
        wRatio = Mathf.Clamp(wRatio, 0f, 0.5f);
        hRatio = Mathf.Clamp(hRatio, 0f, 0.5f);
        byte[] data = File.ReadAllBytes(srcPng);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data);
        int w = tex.width, h = tex.height;
        int mw = Mathf.RoundToInt(w * wRatio);
        int mh = Mathf.RoundToInt(h * hRatio);
        int x0 = Mathf.Max(0, w - mw);
        int y0 = 0; // 텍스처 좌표 하단이 y=0
        int srcX = Mathf.Max(0, x0 - 1); // 마스크 영역 바로 왼쪽 열
        // 마스크 영역의 각 행을, 그 행의 왼쪽 인접 픽셀로 수평 복제한다.
        // (워터마크가 프레임 나무판/밧줄 위에 있어 세로 복제는 결이 어긋나므로 가로 연장이 자연스럽다)
        for (int y = y0; y < y0 + mh; y++)
        {
            Color sample = tex.GetPixel(srcX, y);
            for (int x = x0; x < w; x++)
                tex.SetPixel(x, y, sample);
        }
        tex.Apply();
        File.WriteAllBytes(destPng, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // ── 기존 import된 ItemIcon 일괄 워터마크 크롭 (1회용 — 재실행 시 추가 크롭되니 주의) ──
    [MenuItem("WES/AI Texture/Crop Watermark - ItemIcon (one-time)")]
    public static void CropItemIconWatermarks()
    {
        const string dir = "Assets/GameResource/Image/ItemIcon";
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
        int n = 0;
        foreach (var g in guids)
        {
            string full = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(g));
            CropInsetPng(full, full, 0.08f); // 인플레이스 1회 크롭
            n++;
        }
        AssetDatabase.Refresh();
        NormalizeFolderToSprite(dir);
        PackFolderIntoAtlas(dir, "Icons");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AiTexture] 워터마크 크롭 {n}건(ItemIcon, inset 8%) + 재정규화·아틀라스 완료");
    }
}
