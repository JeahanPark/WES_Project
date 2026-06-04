using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase2 배경·로고 배치 import 유틸.
/// .playwright-output의 다운로드 원본 → 우하단 워터마크 마스킹 →
/// Assets/GameResource/UI/Background 저장·Sprite화.
/// 배경은 풀스크린 단일 이미지(9-slice 불필요, border 0). 아틀라스 미편입
/// (대형 배경은 아틀라스에 넣으면 패킹 비효율 → 개별 Sprite/Addressable 권장).
/// 로고는 투명 배경 워드마크. 톤·생성은 designer 수행, 와이어링은 client.
/// </summary>
public static class UiBackgroundImportSetup
{
    private const string DROP_ROOT = @"C:\GitFork\WES_Project\.playwright-output";
    private const string BG_DIR = "Assets/GameResource/UI/Background";

    // (원본 src stem, 대상 자산명)
    private static readonly (string src, string name)[] BgBatch =
    {
        ("bg_title_src",          "bg_title"),          // I-1 인트로/타이틀 배경
        ("bg_lobby_src",          "bg_lobby"),          // L-1 로비/야영지 배경
        ("bg_clear_success_src",  "bg_clear_success"),  // C-1 탈출 성공 배경
        ("bg_clear_defeat_src",   "bg_clear_defeat"),   // C-2 전멸 배경
        ("logo_main_src",         "logo_main"),         // I-2 게임 로고 워드마크(투명)
    };

    [MenuItem("WES/AI Texture/Import UI Background+Logo Batch (mask)")]
    public static void ImportBackgroundBatch()
    {
        if (!AssetDatabase.IsValidFolder(BG_DIR))
        {
            string parent = Path.GetDirectoryName(BG_DIR).Replace("\\", "/");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(BG_DIR));
        }

        int n = 0;
        foreach (var (src, name) in BgBatch)
        {
            string srcFull = Path.Combine(DROP_ROOT, src + ".png");
            if (!File.Exists(srcFull)) { Debug.LogWarning($"[BgBatch] 원본 없음: {srcFull}"); continue; }

            string destPath = $"{BG_DIR}/{name}.png";
            string destFull = Path.GetFullPath(destPath);
            if (name == "logo_main")
                // 로고는 글자판이 우하단까지 닿아 마스킹 시 훼손 위험 + 원본 워터마크 비가시 → 원본 그대로 복사
                File.Copy(srcFull, destFull, overwrite: true);
            else
                // 배경: 우하단 워터마크만 마스킹(가로 8%·세로 6%). 16:9 코너라 거의 비가시.
                AiTextureImportSetup.MaskCornerWatermarkPng(srcFull, destFull, 0.08f, 0.06f);
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(destPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
            n++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BgBatch] {n}건 마스킹·import 완료 ({BG_DIR})");
    }
}
