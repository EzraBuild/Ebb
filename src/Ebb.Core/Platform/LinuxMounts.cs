using System.Text;

namespace Ebb.Core.Platform;

/// <param name="Device">major:minor of the file system. Btrfs subvolumes of one disk share it.</param>
/// <param name="Root">Which folder of that file system is mounted here: "/" usually, "/@home" for a btrfs subvolume.</param>
public sealed record Mount(string Device, string Root, string Target, string FsType, string Source);

/// <summary>
/// The file systems Linux has mounted, from <c>/proc/self/mountinfo</c>. Used to list drives and to keep a
/// scan on one drive: other disks, /proc and the like, and a second view of the same folders (bind mounts)
/// are skipped. Btrfs subvolumes of the scanned disk, like /home on many setups, are included.
/// </summary>
public static class LinuxMounts
{
    private static readonly HashSet<string> DiskTypes = new(StringComparer.Ordinal)
    {
        "ext2", "ext3", "ext4", "btrfs", "xfs", "f2fs", "zfs", "bcachefs", "jfs", "reiserfs", "nilfs2",
        "ntfs", "ntfs3", "exfat", "vfat", "fuseblk",
    };

    private static readonly string[] NotDrives = ["/boot", "/efi", "/snap", "/var/lib/docker", "/mnt/wsl", "/mnt/wslg"];

    public static IReadOnlyList<Mount> Read(string mountInfo = "/proc/self/mountinfo") =>
        File.Exists(mountInfo) ? Parse(File.ReadAllLines(mountInfo)) : [];

    /// <summary>Parses mountinfo lines: "36 35 98:0 /mnt1 /mnt/parent rw,noatime master:1 - ext3 /dev/root rw".</summary>
    public static IReadOnlyList<Mount> Parse(IEnumerable<string> lines)
    {
        var mounts = new List<Mount>();
        foreach (var line in lines)
        {
            var fields = line.Split(' ');
            var dash = Array.IndexOf(fields, "-");
            if (fields.Length < 7 || dash < 6 || dash + 2 >= fields.Length)
                continue;
            mounts.Add(new Mount(fields[2], Unescape(fields[3]), Unescape(fields[4]), fields[dash + 1], Unescape(fields[dash + 2])));
        }
        return mounts;
    }

    /// <summary>Disks you'd think of as drives: one mount point per disk, the shortest one (usually /).</summary>
    public static IReadOnlyList<Mount> Drives(IReadOnlyList<Mount> mounts) =>
        mounts
            .Where(m => DiskTypes.Contains(m.FsType) && !NotDrives.Any(n => m.Target == n || m.Target.StartsWith(n + "/", StringComparison.Ordinal)))
            .GroupBy(m => m.Device)
            .Select(g => g.OrderBy(m => m.Target.Length).First())
            .OrderBy(m => m.Target == "/" ? "" : m.Target, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// What a scan of <paramref name="root"/> must not go into: mount points of other file systems, and
    /// mounts that show folders already visible elsewhere. Snapshot folders are skipped too, since they
    /// hold copies that would make every file count many times.
    /// </summary>
    public static Func<string, bool> SkipFor(string root, IReadOnlyList<Mount> mounts)
    {
        // The mount the root is on. If two are stacked on one folder, the last one is the one you see.
        var rootMount = mounts.Where(m => IsSameOrUnder(root, m.Target)).OrderBy(m => m.Target.Length).LastOrDefault();
        var skip = new HashSet<string>(StringComparer.Ordinal);
        var kept = rootMount is null ? new List<Mount>() : [rootMount];
        // Shortest first, so a subvolume (/@home) is kept before a folder inside it mounted somewhere else.
        foreach (var mount in mounts.Where(m => IsUnder(m.Target, root)).OrderBy(m => m.Root.Length))
        {
            var sameDisk = rootMount is not null && mount.Device == rootMount.Device;
            var seenAlready = kept.Any(k => k.Device == mount.Device &&
                                            (IsSameOrUnder(mount.Root, k.Root) || IsSameOrUnder(k.Root, mount.Root)));
            if (sameDisk && !seenAlready)
                kept.Add(mount);
            else
                skip.Add(mount.Target);
        }
        return path => skip.Contains(path) || Path.GetFileName(path) is ".snapshots" or "timeshift-btrfs";
    }

    /// <summary>mountinfo writes spaces and a few other characters as octal, like \040.</summary>
    internal static string Unescape(string field)
    {
        if (!field.Contains('\\'))
            return field;
        var text = new StringBuilder(field.Length);
        for (var i = 0; i < field.Length; i++)
        {
            if (field[i] == '\\' && IsOctal(field, i + 1) && IsOctal(field, i + 2) && IsOctal(field, i + 3))
            {
                text.Append((char)Convert.ToInt32(field.Substring(i + 1, 3), 8));
                i += 3;
            }
            else
            {
                text.Append(field[i]);
            }
        }
        return text.ToString();
    }

    private static bool IsOctal(string s, int i) => i < s.Length && s[i] is >= '0' and <= '7';

    // Linux paths, compared as plain strings so this also works in tests on Windows.
    private static bool IsUnder(string path, string folder) =>
        folder == "/" ? path.Length > 1 && path[0] == '/' : path.StartsWith(folder + "/", StringComparison.Ordinal);

    private static bool IsSameOrUnder(string path, string folder) => path == folder || IsUnder(path, folder);
}
