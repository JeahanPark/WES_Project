using System;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace WesQA
{
    /// <summary>Invoke(listener, data) → TestManager.Instance의 listener 이름 public 메서드를
    /// 리플렉션으로 찾아 data(kwargs)를 파라미터에 매핑해 호출. 기존 메서드 호출만(신규 로직 없음).
    /// WesQA(Editor asmdef)는 Assembly-CSharp(TestManager 소속)을 컴파일타임 참조할 수 없으므로
    /// 타입·Instance까지 전부 리플렉션으로 해석한다.</summary>
    public static class InvokeBridge
    {
        private static Type s_TestManagerType;

        private static Type ResolveTestManagerType()
        {
            if (s_TestManagerType != null) return s_TestManagerType;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType("TestManager", false); }
                catch { t = null; }
                if (t != null) { s_TestManagerType = t; return t; }
            }
            throw new Exception("TestManager type not found in any loaded assembly");
        }

        private static object ResolveInstance(Type tmType)
        {
            // MonoSingleton<T>.Instance (public static property) 우선
            var prop = tmType.GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop != null) return prop.GetValue(null);

            var field = tmType.GetField(
                "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field != null) return field.GetValue(null);

            throw new Exception("TestManager.Instance accessor not found");
        }

        public static object Invoke(string listener, JObject data)
        {
            if (string.IsNullOrEmpty(listener)) throw new Exception("listener is null/empty");

            var tmType = ResolveTestManagerType();
            var tm = ResolveInstance(tmType);
            if (tm == null) throw new Exception("TestManager.Instance is null");

            var method = tmType.GetMethod(
                listener, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (method == null) throw new Exception("no such TestManager method: " + listener);

            var ps = method.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                JToken tok = data != null ? data[p.Name] : null;
                if (tok == null && data != null && data.Count == ps.Length)
                {
                    int idx = 0;
                    foreach (var prop in data.Properties())
                    {
                        if (idx == i) { tok = prop.Value; break; }
                        idx++;
                    }
                }
                if (tok != null) args[i] = tok.ToObject(p.ParameterType);
                else if (p.HasDefaultValue) args[i] = p.DefaultValue;
                else args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }

            return method.Invoke(tm, args); // void면 null
        }
    }
}
