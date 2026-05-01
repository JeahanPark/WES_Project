#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TempAutoRunner
{
    private const string SESSION_KEY = "MapGen_AutoBake_v2_AfterLandmark";

    static TempAutoRunner()
    {
        if (!SessionState.GetBool(SESSION_KEY, false))
        {
            SessionState.SetBool(SESSION_KEY, true);
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[TempAutoRunner] NavMesh 재베이크 자동 실행");
                MapGenerator.BakeNavMesh();
            };
        }
    }
}
#endif
