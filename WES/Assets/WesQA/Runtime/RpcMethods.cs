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
                case "Click":
                    return InputInjector.Click(D(req, 0), D(req, 1));
                case "RClick":
                    return InputInjector.RClick(D(req, 0), D(req, 1));
                case "DoubleClick":
                    return InputInjector.DoubleClick(D(req, 0), D(req, 1));
                case "KeyEvent":
                    return InputInjector.KeyEvent(S(req, 0));
                case "SetText":
                {
                    var a = req.Args();
                    long id = a.Count > 0 ? a[0].ToObject<long>() : 0;
                    string txt = a.Count > 1 ? a[1].ToObject<string>() : "";
                    return InputInjector.SetText(id, txt);
                }
                default:
                    throw new NotSupportedException($"unknown method: {req.Method}");
            }
        }

        private static double D(RpcRequest req, int i)
        {
            var a = req.Args();
            return i < a.Count ? a[i].ToObject<double>() : 0.0;
        }

        private static string S(RpcRequest req, int i)
        {
            var a = req.Args();
            return i < a.Count ? a[i].ToObject<string>() : null;
        }
    }
}
