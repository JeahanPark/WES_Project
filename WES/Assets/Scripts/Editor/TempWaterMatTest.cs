#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TempWaterMatTest
{
    private const string SESSION_KEY = "WaterMatTest_v1";

    static TempWaterMatTest()
    {
        if (!SessionState.GetBool(SESSION_KEY, false))
        {
            SessionState.SetBool(SESSION_KEY, true);
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[TempWaterMatTest] Water 머터리얼을 URP/Lit Opaque로 임시 교체");

                var deepWater = GameObject.Find("DeepWater");
                if (deepWater == null) { Debug.LogError("[Test] DeepWater 없음"); return; }

                var mr = deepWater.GetComponent<MeshRenderer>();
                if (mr == null) { Debug.LogError("[Test] MeshRenderer 없음"); return; }

                // URP Lit shader 찾기
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit == null)
                {
                    Debug.LogError("[Test] URP Lit shader 없음");
                    return;
                }

                // 새 머터리얼 생성
                var testMat = new Material(urpLit);
                testMat.name = "TempWaterTest";
                testMat.SetColor("_BaseColor", new Color(0.27f, 0.47f, 0.67f, 1f));  // 기존과 비슷한 청록
                testMat.SetFloat("_Surface", 0);  // Opaque
                testMat.renderQueue = 2000;

                // 머터리얼 적용
                mr.sharedMaterial = testMat;

                Debug.Log("[TempWaterMatTest] 머터리얼 교체 완료. 게임뷰 확인.");
            };
        }
    }
}
#endif
