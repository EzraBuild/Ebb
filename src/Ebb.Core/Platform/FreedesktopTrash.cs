using System.Globalization;
using System.Runtime.InteropServices;
using Ebb.Core.Cleanup;
using Ebb.Core.Scanning;

namespace Ebb.Core.Platform;

/// <summary>
/// The Linux Trash, the way file managers use it: each item moves to <c>files/</c> and gets a <c>.trashinfo</c>
/// note in <c>info/</c>, so it can be restored to where it was. Things on your home drive go to
/// <c>~/.local/share/Trash</c>; things on another drive (a USB stick, a second disk) go to that drive's own
/// <c>.Trash-1000</c> folder, so nothing has to be copied across drives.
/// </summary>
public sealed class FreedesktopTrash : IRecycleBin
{
    private readonly string _home;
    private readonly Func<string, Mount?> _mountOf;
    private readonly string _uid;

    /// <param name="homeTrash"><c>~/.local/share/Trash</c></param>
    public FreedesktopTrash(string homeTrash)
        : this(homeTrash, path => LinuxMounts.MountOf(path, LinuxMounts.Read()), LinuxUser.Id)
    {
    }

    /// <param name="mountOf">The mount a path is on, or null if unknown (then everything goes to the home Trash).</param>
    /// <param name="uid">Your user id, which names the Trash folders on other drives.</param>
    internal FreedesktopTrash(string homeTrash, Func<string, Mount?> mountOf, string uid)
    {
        _home = homeTrash;
        _mountOf = mountOf;
        _uid = uid;
    }

    /// <param name="trash">A Trash folder: the home one, or one on another drive. Empty means the home Trash.</param>
    public RecycleBinInfo Query(string trash)
    {
        var files = Path.Combine(Folder(trash), "files");
        if (!Directory.Exists(files))
            return new RecycleBinInfo(0, 0);
        var items = Directory.EnumerateFileSystemEntries(files).Count();
        return new RecycleBinInfo(DirectorySizer.Measure(files), items);
    }

    /// <summary>Deletes everything in one Trash folder for good.</summary>
    public void Empty(string trash)
    {
        var root = Folder(trash);
        foreach (var folder in new[] { Path.Combine(root, "files"), Path.Combine(root, "info") })
        {
            if (!Directory.Exists(folder))
                continue;
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo directory && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    FolderDelete.Run(directory.FullName);
                else
                    entry.Delete();
            }
        }
        var sizes = Path.Combine(root, "directorysizes");
        if (File.Exists(sizes))
            File.Delete(sizes);
    }

    /// <summary>Your Trash folders on the drive mounted at <paramref name="topdir"/>, if there are any.</summary>
    public static IEnumerable<string> OnDrive(string topdir, string uid) =>
        new[] { Path.Combine(topdir, ".Trash", uid), Path.Combine(topdir, $".Trash-{uid}") }.Where(Directory.Exists);

    /// <summary>Moves one file or folder to the Trash on its own drive.</summary>
    public void MoveToTrash(string path, DateTime? now = null)
    {
        var (trash, topdir) = TrashFor(path);
        var files = Path.Combine(trash, "files");
        var info = Path.Combine(trash, "info");
        CreatePrivate(trash);
        Directory.CreateDirectory(files);
        Directory.CreateDirectory(info);

        // On another drive the note holds the path relative to that drive, so it still works if it's mounted elsewhere.
        var original = topdir is null ? path : Path.GetRelativePath(topdir, path);
        var deletedAt = (now ?? DateTime.Now).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var (note, trashed) = Reserve(files, info, Path.GetFileName(path), original, deletedAt);
        try
        {
            if (Directory.Exists(path))
                Directory.Move(path, trashed);
            else
                File.Move(path, trashed);
        }
        catch (IOException ex)
        {
            File.Delete(note);
            throw new IOException(
                $"Couldn't move it to the Trash ({ex.Message}). Untick “Move to Trash” to delete it for good instead.", ex);
        }
    }

    /// <summary>Which Trash a path goes to, and the drive's mount point when it isn't the home Trash.</summary>
    private (string Trash, string? Topdir) TrashFor(string path)
    {
        var mount = _mountOf(path);
        var home = _mountOf(_home);
        if (mount is null || home is null || mount.Device == home.Device)
            return (_home, null);

        // The spec's shared .Trash only counts as a real folder (not a link) with the sticky bit, so users
        // can't touch each other's things. Otherwise each user gets their own .Trash-<uid>.
        var shared = Path.Combine(mount.Target, ".Trash");
        return IsSharedTrash(shared)
            ? (Path.Combine(shared, _uid), mount.Target)
            : (Path.Combine(mount.Target, $".Trash-{_uid}"), mount.Target);
    }

    private static bool IsSharedTrash(string folder) =>
        !OperatingSystem.IsWindows() && Directory.Exists(folder) &&
        !File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint) &&
        File.GetUnixFileMode(folder).HasFlag(UnixFileMode.StickyBit);

    private static void CreatePrivate(string folder)
    {
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(folder);
        else
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Writes the .trashinfo note first under a free name, so two items never get the same one.</summary>
    private static (string Note, string Trashed) Reserve(string files, string info, string name, string original, string deletedAt)
    {
        var text = $"[Trash Info]\nPath={EncodePath(original)}\nDeletionDate={deletedAt}\n";
        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? name : $"{name}.{n}";
            var note = Path.Combine(info, candidate + ".trashinfo");
            var trashed = Path.Combine(files, candidate);
            if (File.Exists(trashed) || Directory.Exists(trashed))
                continue;
            try
            {
                using var file = new FileStream(note, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(file);
                writer.Write(text);
                return (note, trashed);
            }
            catch (IOException) when (File.Exists(note))
            {
                // taken; try the next name
            }
        }
    }

    /// <summary>The original path, URL-escaped per folder name as the Trash spec asks.</summary>
    internal static string EncodePath(string path) =>
        string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));

    private string Folder(string trash) => string.IsNullOrEmpty(trash) ? _home : trash;
}

/// <summary>The Linux user Ebb runs as.</summary>
public static class LinuxUser
{
    private static readonly Lazy<string> Uid = new(() => OperatingSystem.IsWindows() ? "0" : getuid().ToString(CultureInfo.InvariantCulture));

    /// <summary>Your numeric user id, like "1000".</summary>
    public static string Id => Uid.Value;

    [DllImport("libc")]
    private static extern uint getuid();
}
