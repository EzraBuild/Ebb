using Ebb.Core;
using Ebb.Core.Findings;

namespace Ebb.App.ViewModels;

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

    /// <summary>Shows paths under your user folder as ~\...</summary>
    public static string ShortPath(string path, string userProfile) =>
        PathUtil.IsUnder(path, userProfile) ? "~\\" + System.IO.Path.GetRelativePath(userProfile, path) : path;

    public static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n:N0} {many}";
}
