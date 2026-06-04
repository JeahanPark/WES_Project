using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase2 UI 프레임 세트(wes-ui-frame) 배치 import 유틸.
/// .playwright-output의 다운로드 원본 → 우하단 Gemini 워터마크만 마스킹(9-slice 모서리 보존)
/// → Assets/GameResource/UI/Frame 저장·Sprite화·Ui 아틀라스 편입 → 9-slice border 설정.
/// 9-slice는 전방위 inset 크롭 시 못·밧줄 매듭 모서리 디테일이 깎이므로
/// CropInsetPng가 아닌 MaskCornerWatermarkPng를 쓴다. 톤·생성은 designer 수행.
/// </summary>
public static class UiFrameImportSetup
{
    private const string DROP_ROOT = @"C:\GitFork\WES_Project\.playwright-output";
    private const string FRAME_DIR = "Assets/GameResource/UI/Frame";

    // (원본 src stem, 대상 자산명, 9-slice border 좌하우상 px)
    // 표본 600² 기준. 버튼은 작은 사이즈(예 200x60)에 적용되므로 border 과다 시 테두리가
    // 압축돼 뭉개진다(QA A7/LOW) → 버튼 3종 border 40으로 낮춰 작은 버튼에서도 나무 테두리
    // 디테일 유지. 큰 셀에 적용되는 slot/panel은 기존 유지(문제 없음).
    private static readonly (string src, string name, Vector4 border)[] FrameBatch =
    {
        ("frame_btn_idle_src",     "btn_frame_idle",     new Vector4(40, 40, 40, 40)),
        ("frame_btn_hover_src",    "btn_frame_hover",    new Vector4(40, 40, 40, 40)),
        ("frame_btn_disabled_src", "btn_frame_disabled", new Vector4(40, 40, 40, 40)),
        ("frame_panel_src",        "panel_frame",        new Vector4(120, 120, 120, 120)),
        ("frame_slot_src",         "slot_frame",         new Vector4(90,  90,  90,  90)),
    };

    [MenuItem("WES/AI Texture/Import UI Frame Batch (mask+9slice)")]
    public static void ImportFrameBatch()
    {
        if (!AssetDatabase.IsValidFolder(FRAME_DIR))
        {
            // GameResource/UI 까지는 존재한다는 전제, Frame leaf만 생성
            string parent = Path.GetDirectoryName(FRAME_DIR).Replace("\\", "/");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(FRAME_DIR));
        }

        int n = 0;
        foreach (var (src, name, border) in FrameBatch)
        {
            string srcFull = Path.Combine(DROP_ROOT, src + ".png");
            if (!File.Exists(srcFull)) { Debug.LogWarning($"[FrameBatch] 원본 없음: {srcFull}"); continue; }

            string destPath = $"{FRAME_DIR}/{name}.png";
            string destFull = Path.GetFullPath(destPath);
            // 우하단 워터마크만 마스킹(가로 6%·세로 5%) — 모서리 매듭/못 디테일 보존
            AiTextureImportSetup.MaskCornerWatermarkPng(srcFull, destFull, 0.06f, 0.05f);
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(destPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.spriteBorder = border;
            ti.SaveAndReimport();
            n++;
        }

        AssetDatabase.Refresh();
        AiTextureImportSetup.PackFolderIntoAtlas(FRAME_DIR, "Ui");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FrameBatch] {n}건 마스킹·import·9-slice·Ui 아틀라스 완료 ({FRAME_DIR})");
    }
}
