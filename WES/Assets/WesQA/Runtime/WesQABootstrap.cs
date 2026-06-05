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

        // M1: 단일 인스턴스 = 0. 멀티클라(MPPM) index 해석은 M4에서 구현.
        private static int ResolveInstanceIndex() => 0;
    }
}
