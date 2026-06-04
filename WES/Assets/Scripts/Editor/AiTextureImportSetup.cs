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

    // ── 스모크 검증용 메뉴 (아틀라스 생성·폴더 편입만 단독 확인) ──
    [MenuItem("WES/AI Texture/Smoke - Create Icons Atlas From ItemIcon")]
    public static void SmokeCreateIconsAtlas()
    {
        PackFolderIntoAtlas("Assets/GameResource/Image/ItemIcon", "Icons");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AiTexture] smoke: Icons.spriteatlas created/updated with ItemIcon folder");
    }
}
