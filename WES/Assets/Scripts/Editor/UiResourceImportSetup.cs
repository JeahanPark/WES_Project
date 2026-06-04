using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 2 UI 리소스(프레임·배경·아이콘) AI 생성분의 import·9-slice 표준화 유틸.
/// AiTextureImportSetup(복사·Sprite화·아틀라스 편입)을 감싸고, UI 프레임용
/// 9-slice border 설정을 추가한다. 톤·실제 생성은 designer가 수행.
/// </summary>
public static class UiResourceImportSetup
{
    // 디자이너가 다운로드한 PNG를 모아두는 임시 폴더 (Playwright 출력 회수처)
    public const string DROP_DIR = "Assets/GameResource/_AiDrop";

    /// <summary>
    /// _AiDrop 폴더의 PNG들을 매핑표대로 카테고리 폴더로 들여오고 아틀라스에 편입한다.
    /// 매핑은 파일명(확장자 제외) → (categoryDir, atlasName) 규칙으로 코드에 명시.
    /// 단순 1회 호출용이라 designer가 필요 시 매핑을 갱신한다.
    /// </summary>
    [MenuItem("WES/AI Texture/Import UI Drop (frames+backgrounds)")]
    public static void ImportUiDrop()
    {
        string dropFull = Path.GetFullPath(DROP_DIR);
        if (!Directory.Exists(dropFull))
        {
            Debug.LogWarning($"[UiImport] drop 폴더 없음: {DROP_DIR}");
            return;
        }

        int count = 0;
        foreach (var png in Directory.GetFiles(dropFull, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(png);
            (string dir, string atlas, Vector4 border) = Resolve(name);
            string dest = AiTextureImportSetup.ImportAndPack(png, dir, name, atlas);
            if (border != Vector4.zero)
                SetSpriteBorder(dest, border);
            count++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[UiImport] {count}건 import 완료 (drop={DROP_DIR})");
    }

    /// <summary>
    /// 자산명 접두로 카테고리·아틀라스·9-slice border를 결정한다.
    /// frame/slot/panel 류는 Frame 폴더 + Ui 아틀라스 + border, 배경류는 Background 폴더.
    /// </summary>
    private static (string dir, string atlas, Vector4 border) Resolve(string name)
    {
        // 9-slice 프레임류 (좌하우상 동일 px — 표본 512² 기준 48px 권장, 후 수동 조정 가능)
        if (name.StartsWith("btn_frame") || name.StartsWith("panel_frame") ||
            name.StartsWith("slot_frame") || name.Contains("_frame"))
            return ("Assets/GameResource/UI/Frame", "Ui", new Vector4(48, 48, 48, 48));

        // 배경/타이틀/결과화면 등 늘리지 않는 이미지
        if (name.Contains("_bg") || name.Contains("background") ||
            name.Contains("title") || name.Contains("logo") || name.Contains("result"))
            return ("Assets/GameResource/UI/Background", "", Vector4.zero);

        // 기본: 아이콘으로 간주
        return ("Assets/GameResource/Image/ItemIcon", "Icons", Vector4.zero);
    }

    /// <summary>Sprite의 9-slice border(좌하우상 px)를 설정한다.</summary>
    public static void SetSpriteBorder(string assetPath, Vector4 border)
    {
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null) return;
        ti.spriteBorder = border;
        ti.SaveAndReimport();
    }

    // ── Phase2 ItemIcon 배치 import (다운로드 원본 → 워터마크 크롭 → ItemIcon 저장) ──
    // .playwright-output의 비결정적 파일명을 아이콘명에 매핑한다. 멱등성: 항상 원본에서 크롭.
    private const string DROP_ROOT = @"C:\GitFork\WES_Project\.playwright-output";
    private const string ICON_DIR = "Assets/GameResource/Image/ItemIcon";

    // (원본PNG파일명stem, 대상아이콘명) — 배치마다 designer가 갱신
    private static readonly (string src, string icon)[] IconBatch =
    {
        // 배치2 — 소비/장비 (배치1 자원6종은 이미 import 완료)
        ("Gemini-Generated-Image-16isa216isa216is", "torch_icon"),
        ("Gemini-Generated-Image-6b9okx6b9okx6b9o", "bandage_icon"),
        ("Gemini-Generated-Image-z9s1ubz9s1ubz9s1", "potion_cold_icon"),
        ("Gemini-Generated-Image-ifa8hbifa8hbifa8", "sword_icon"),
        ("Gemini-Generated-Image-exiy8kexiy8kexiy", "ironsword_icon"),
        ("Gemini-Generated-Image-gslbgzgslbgzgslb", "shield_icon"),
        ("Gemini-Generated-Image-1yaqlb1yaqlb1yaq", "leatherarmor_icon"),
    };

    [MenuItem("WES/AI Texture/Import ItemIcon Batch (crop+pack)")]
    public static void ImportItemIconBatch()
    {
        int n = 0;
        foreach (var (src, icon) in IconBatch)
        {
            string srcFull = Path.Combine(DROP_ROOT, src + ".png");
            if (!File.Exists(srcFull)) { Debug.LogWarning($"[IconBatch] 원본 없음: {srcFull}"); continue; }
            string destFull = Path.GetFullPath($"{ICON_DIR}/{icon}.png");
            AiTextureImportSetup.CropInsetPng(srcFull, destFull, 0.08f); // 원본→크롭→ItemIcon
            AssetDatabase.ImportAsset($"{ICON_DIR}/{icon}.png", ImportAssetOptions.ForceUpdate);
            n++;
        }
        AssetDatabase.Refresh();
        AiTextureImportSetup.NormalizeFolderToSprite(ICON_DIR);
        AiTextureImportSetup.PackFolderIntoAtlas(ICON_DIR, "Icons");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[IconBatch] {n}건 크롭·import·아틀라스 완료 (inset 8%)");
    }
}
