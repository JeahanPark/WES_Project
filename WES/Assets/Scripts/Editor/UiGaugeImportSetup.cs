using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase2 게이지 스킨(HP/스태미나/추위) 배치 import 유틸.
/// .playwright-output의 다운로드 원본 → 우하단 워터마크 마스킹 →
/// Assets/GameResource/UI/Gauge 저장·Sprite화·Ui 아틀라스 편입.
/// 빈 바 프레임은 가로 9-slice(좌우 엔드캡 보존, 중앙 가로로 늘어남),
/// fill 3종은 가로 fillAmount 마스킹용 단순 Sprite(끝 잘림 방지용 소량 가로 border).
/// fill 색은 리소스리스트 G-1 원칙상 Procedural도 가능하나, HP/추위 시각 차별(피/서리)을
/// 위해 텍스처 스킨으로 제공한다. 와이어링(Image type=Filled 등)은 client 영역.
/// </summary>
public static class UiGaugeImportSetup
{
    private const string DROP_ROOT = @"C:\GitFork\WES_Project\.playwright-output";
    private const string GAUGE_DIR = "Assets/GameResource/UI/Gauge";

    // (원본 src stem, 대상 자산명, 9-slice border 좌하우상 px)
    // 표본 600×300 기준. 빈 프레임은 둥근 엔드캡(좌우 ~120px) 보존, 상하 채널 ~70px.
    // fill은 좌우 끝만 살짝(30px) 줘서 가로 stretch/fill 시 가장자리 결이 자연스럽게.
    private static readonly (string src, string name, Vector4 border)[] GaugeBatch =
    {
        ("gauge_empty_src",        "gauge_frame_empty",  new Vector4(120, 70, 120, 70)),
        ("gauge_hp_fill_src",      "gauge_fill_hp",      new Vector4(30, 0, 30, 0)),
        ("gauge_stamina_fill_src", "gauge_fill_stamina", new Vector4(30, 0, 30, 0)),
        ("gauge_cold_fill_src",    "gauge_fill_cold",    new Vector4(30, 0, 30, 0)),
    };

    [MenuItem("WES/AI Texture/Import UI Gauge Batch (mask+9slice)")]
    public static void ImportGaugeBatch()
    {
        if (!AssetDatabase.IsValidFolder(GAUGE_DIR))
        {
            string parent = Path.GetDirectoryName(GAUGE_DIR).Replace("\\", "/");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(GAUGE_DIR));
        }

        int n = 0;
        foreach (var (src, name, border) in GaugeBatch)
        {
            string srcFull = Path.Combine(DROP_ROOT, src + ".png");
            if (!File.Exists(srcFull)) { Debug.LogWarning($"[GaugeBatch] 원본 없음: {srcFull}"); continue; }

            string destPath = $"{GAUGE_DIR}/{name}.png";
            string destFull = Path.GetFullPath(destPath);
            if (name == "gauge_frame_empty")
                // 프레임: 우하단 워터마크만 마스킹(엔드캡·리벳 디테일 보존)
                AiTextureImportSetup.MaskCornerWatermarkPng(srcFull, destFull, 0.06f, 0.08f);
            else
                // fill: 상하 30% 크롭으로 검은 여백 제거(스트립만 남김) + 하단 워터마크 동시 제거
                AiTextureImportSetup.CropVerticalInsetPng(srcFull, destFull, 0.30f);
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
        AiTextureImportSetup.PackFolderIntoAtlas(GAUGE_DIR, "Ui");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GaugeBatch] {n}건 마스킹·import·9-slice·Ui 아틀라스 완료 ({GAUGE_DIR})");
    }
}
