using System.Diagnostics;
using Debug = UnityEngine.Debug;

public static class GameDebug
{
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object _message)
    {
        Debug.Log(_message);
    }

    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object _message)
    {
        Debug.LogWarning(_message);
    }

    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object _message)
    {
        Debug.LogError(_message);
    }
}
