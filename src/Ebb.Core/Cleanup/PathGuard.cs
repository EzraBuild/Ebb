using Ebb.Core.Platform;

namespace Ebb.Core.Cleanup;

/// <summary>
/// The last line of defence: decides if Ebb may remove a path at all. Checked when the plan is made
/// and again right before each removal.
/// </summary>
public sealed class PathGuard
{
    private readonly bool _linux;
    private readonly string _systemDrive;
    private readonly IReadOnlyList<string> _systemTrees;
    private readonly string[] _keepExactly;
    private readonly string _usersFolder;
    private readonly string _profile;
    private readonly string? _trash;
    private readonly string? _confineTo;

    /// <param name="paths">Where the system keeps things.</param>
    /// <param name="confineTo">If set, nothing outside this folder may ever be removed (used by tests).</param>
    public PathGuard(SystemPaths paths, string? confineTo = null)
    {
        _linux = paths.IsLinux;
        _systemDrive = paths.SystemDrive;
        _systemTrees = paths.SystemFolders;
        _keepExactly = paths.IsLinux
            ?
            [
                paths.UserProfile, .. paths.AppDataFolders, paths.Data, paths.Trash, paths.Temp, paths.Downloads,
                .. paths.PersonalFolders,
            ]
            :
            [
                paths.UserProfile, paths.LocalAppData, paths.RoamingAppData, Path.GetDirectoryName(paths.LocalAppData)!,
                paths.Temp, paths.Downloads, .. paths.PersonalFolders,
            ];
        _profile = paths.UserProfile;
        _usersFolder = Path.GetDirectoryName(paths.UserProfile)!;
        _trash = paths.IsLinux ? paths.Trash : null;
        _confineTo = confineTo;
    }

    /// <summary>Null if the path may be removed, otherwise the reason it may not.</summary>
    public string? Check(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.IndexOfAny(['*', '?']) >= 0)
            return "Not a full path.";

        var full = PathUtil.Normalize(path);
        if (_confineTo is not null && !PathUtil.IsUnder(full, _confineTo))
            return "Outside the allowed folder.";

        var root = PathUtil.IsUnder(full, _systemDrive) ? _systemDrive : Path.GetPathRoot(full)!;
        if (PathUtil.RelativeSegments(full, root).Length < 2)
            return "Top-level folders and files on a drive are never removed.";

        if (_systemTrees.Any(tree => PathUtil.IsSameOrUnder(full, tree)))
            return _linux
                ? "System and package folders are never touched directly."
                : "Windows and app install folders are never touched directly.";

        if (!_linux && full.Split(Path.DirectorySeparatorChar).Any(s =>
                s.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)))
            return "Windows manages this folder.";

        if (_trash is not null && PathUtil.IsUnder(full, _trash))
            return "It's in the Trash. Empty the Trash instead.";

        if (_keepExactly.Any(keep => PathUtil.SamePath(full, keep)))
            return "This is one of your main folders. Ebb only removes things inside it.";

        if (PathUtil.IsUnder(full, _usersFolder) && !PathUtil.IsUnder(full, _profile))
            return "Belongs to another user.";

        if (!File.Exists(full) && !Directory.Exists(full))
            return "It's already gone.";

        var attributes = File.GetAttributes(full);
        if (attributes.HasFlag(FileAttributes.System))
            return "Windows marks this as a system file.";
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
            return "It's a link to somewhere else.";

        return null;
    }
}
