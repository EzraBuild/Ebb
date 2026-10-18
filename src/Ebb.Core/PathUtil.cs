namespace Ebb.Core;

/// <summary>Path helpers. Case-insensitive on Windows, case-sensitive on Linux, like the file systems.</summary>
public static class PathUtil
{
    /// <summary>How paths and file names compare on this system.</summary>
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Full path without a trailing separator (except for roots like <c>C:\</c> or <c>/</c>).</summary>
    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var rootLength = Path.GetPathRoot(full)?.Length ?? 0;
        return full.Length > rootLength
            ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : full;
    }

    public static bool SamePath(string a, string b) => string.Equals(Normalize(a), Normalize(b), Comparison);

    /// <summary>True when <paramref name="path"/> is strictly inside <paramref name="folder"/>.</summary>
    public static bool IsUnder(string path, string folder)
    {
        var p = Normalize(path);
        var f = Normalize(folder);
        var prefix = f.EndsWith(Path.DirectorySeparatorChar) ? f : f + Path.DirectorySeparatorChar;
        return p.Length > prefix.Length && p.StartsWith(prefix, Comparison);
    }

    public static bool IsSameOrUnder(string path, string folder) => SamePath(path, folder) || IsUnder(path, folder);

    /// <summary>Folder names between <paramref name="folder"/> and <paramref name="path"/>.</summary>
    public static string[] RelativeSegments(string path, string folder)
    {
        var relative = Path.GetRelativePath(Normalize(folder), Normalize(path));
        return relative == "." ? [] : relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }
}
