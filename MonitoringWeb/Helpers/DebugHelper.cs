using System.Diagnostics;

namespace MonitoringWeb.Helpers;

public static class DebugHelper
{
    public static string GetCurrentClassMethodAndLine()
    {
        var stackFrame = new StackTrace(true).GetFrame(1); // 1 to skip the helper method itself
        var method = stackFrame!.GetMethod();

        string fileName = stackFrame!.GetFileName() ?? "unknown";
        string methodName = method!.Name ?? "unknown";
        int lineNumber = stackFrame!.GetFileLineNumber();

        return $"File: {fileName}, Method: {methodName}, Line: {lineNumber}";
    }
}
