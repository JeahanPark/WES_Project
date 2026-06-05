using System;

namespace WesQA
{
    /// <summary>RPC 메서드 라우팅. 모든 핸들러는 메인스레드에서 호출됨(Unity API 안전).</summary>
    public static class RpcMethods
    {
        public const string SdkVersion = "wesqa-0.1";

        public static object Invoke(RpcRequest req)
        {
            switch (req.Method)
            {
                case "GetSDKVersion":
                    return SdkVersion;
                case "GetScreenSize":
                    return new[] { (object)UnityEngine.Screen.width, UnityEngine.Screen.height };
                case "Dump":
                {
                    bool onlyVisible = true;
                    var args = req.Args();
                    if (args.Count >= 1) onlyVisible = args[0].ToObject<bool>();
                    return HierarchyDumper.Dump(onlyVisible);
                }
                default:
                    throw new NotSupportedException($"unknown method: {req.Method}");
            }
        }
    }
}
