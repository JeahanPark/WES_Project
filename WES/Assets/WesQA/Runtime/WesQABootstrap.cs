using UnityEngine;

namespace WesQA
{
    /// <summary>플레이모드 진입 시 WesPocoServer를 자동 기동. Editor 전용 어셈블리(릴리스 빌드 미포함).</summary>
    public static class WesQABootstrap
    {
        private static WesPocoServer _server;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            int instance = ResolveInstanceIndex();
            _server = new WesPocoServer(5001 + instance);
            _server.Start();
            Debug.Log($"[WesQA] server starting on port {5001 + instance}");
        }

        // M4: MPPM 가상 플레이어 태그 `wes<N>`(예 wes1, wes2)에서 N을 파싱해 인덱스로 사용.
        // 메인 에디터(태그 없음/미매칭)·예외·미사용 = 0. 항상 안전하게 0 폴백.
        private static int ResolveInstanceIndex()
        {
            try
            {
                foreach (var tag in GetMppmTags())
                {
                    if (!string.IsNullOrEmpty(tag) && tag.StartsWith("wes") &&
                        int.TryParse(tag.Substring(3), out int n))
                        return n;
                }
            }
            catch { }
            return 0;
        }

        // 현재 MPPM 가상 플레이어에 부여된 태그 배열을 반환. 실패/미사용 시 빈 배열.
        // API: Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags() (어셈블리 Unity.Multiplayer.Playmode)
        private static string[] GetMppmTags()
        {
            try { return Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags() ?? new string[0]; }
            catch { return new string[0]; }
        }
    }
}
