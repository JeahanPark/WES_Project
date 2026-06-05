using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// 디자이너 합성 렌더 자가검수용 임시 프리뷰 도구.
/// 추위 오버레이 3종 알파를 단계 목표값(코드 캡 0.85 반영)으로 강제 세팅/복원해
/// Game View에서 풀스크린 합성(중앙 투명여백·서리 가장자리)을 눈으로 확인한다.
/// 게임 로직 무관 — Image 알파만 만지는 시각 검수 전용. 검수 후 Restore로 0 복원.
/// </summary>
public static class CoreTensionPreviewSetup
{
    private const string BASE = "Canvas/InGameHUDWorker/CoreTensionOverlay";

    [MenuItem("WES/AI Texture/Preview Cold Stage3 (visual self-review)")]
    public static void PreviewStage3()
    {
        SetAlpha("ColdOverlay1", 1f);   // 텍스처 자체 알파만 노출(단계1 누적색)
        SetAlpha("ColdOverlay2", 1f);   // 단계2 누적색
        SetAlpha("ColdOverlay3", 0.85f); // 단계3 = 코드 캡
        Debug.Log("[CoreTensionPreview] 추위 3단계 합성 프리뷰 ON. 검수 후 Restore 호출.");
    }

    [MenuItem("WES/AI Texture/Restore Cold Overlays To 0")]
    public static void Restore()
    {
        SetAlpha("ColdOverlay1", 0f);
        SetAlpha("ColdOverlay2", 0f);
        SetAlpha("ColdOverlay3", 0f);
        Debug.Log("[CoreTensionPreview] 추위 오버레이 알파 0 복원 완료.");
    }

    static void SetAlpha(string child, float a)
    {
        var go = GameObject.Find($"{BASE}/{child}");
        if (go == null) { Debug.LogWarning($"[CoreTensionPreview] {child} 없음."); return; }
        var img = go.GetComponent<Image>();
        if (img == null) return;
        var c = img.color; c.a = a; img.color = c;
        EditorUtility.SetDirty(img);
    }
}
