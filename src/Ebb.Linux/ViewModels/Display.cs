using Ebb.Core;
using Ebb.Core.Findings;

namespace Ebb.Linux.ViewModels;

/// <summary>Small formatting helpers shared by the view models.</summary>
internal static class Display
{
    public static string Size(long? bytes) => bytes is { } b ? ByteSize.Format(b) : "—";

    public static string SafetyLabel(Safety safety) => safety switch
    {
        Safety.Safe => "SAFE",
        Safety.CheckFirst => "CHECK FIRST",
        _ => "KEEP",
    };

    /// <summary>Shows paths under your home folder as ~/...</summary>
    public static string ShortPath(string path, string home) =>
        PathUtil.IsUnder(path, home) ? "~/" + Path.GetRelativePath(home, path).Replace('\\', '/') : path;

    public static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n:N0} {many}";
}
