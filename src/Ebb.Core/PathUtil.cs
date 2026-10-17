namespace Ebb.Core;

/// <summary>Case-insensitive Windows path helpers.</summary>
public static class PathUtil
{
    /// <summary>Full path without a trailing separator (except for drive roots like <c>C:\</c>).</summary>
    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var rootLength = Path.GetPathRoot(full)?.Length ?? 0;
        return full.Length > rootLength
            ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : full;
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="path"/> is strictly inside <paramref name="folder"/>.</summary>
    public static bool IsUnder(string path, string folder)
    {
        var p = Normalize(path);
        var f = Normalize(folder);
        var prefix = f.EndsWith(Path.DirectorySeparatorChar) ? f : f + Path.DirectorySeparatorChar;
        return p.Length > prefix.Length && p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSameOrUnder(string path, string folder) => SamePath(path, folder) || IsUnder(path, folder);

    /// <summary>Folder names between <paramref name="folder"/> and <paramref name="path"/>.</summary>
    public static string[] RelativeSegments(string path, string folder)
    {
        var relative = Path.GetRelativePath(Normalize(folder), Normalize(path));
        return relative == "." ? [] : relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }
}
