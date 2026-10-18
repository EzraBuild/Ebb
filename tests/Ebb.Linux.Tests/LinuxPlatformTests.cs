using Ebb.Core.Platform;

namespace Ebb.Linux.Tests;

public class LinuxPlatformTests
{
    // An Omarchy-style btrfs layout, plus the usual system mounts, a USB stick and what WSL adds.
    private static readonly string[] MountInfo =
    [
        "23 1 0:25 /@ / rw,relatime shared:1 - btrfs /dev/nvme0n1p2 rw,compress=zstd:1",
        "24 23 0:25 /@home /home rw,relatime shared:2 - btrfs /dev/nvme0n1p2 rw,compress=zstd:1",
        "25 23 0:25 /@log /var/log rw,relatime shared:3 - btrfs /dev/nvme0n1p2 rw",
        "26 23 0:25 /@pkg /var/cache/pacman/pkg rw,relatime shared:4 - btrfs /dev/nvme0n1p2 rw",
        "27 23 0:25 /@snapshots /.snapshots rw,relatime shared:5 - btrfs /dev/nvme0n1p2 rw",
        "28 23 259:1 / /boot rw,relatime shared:6 - vfat /dev/nvme0n1p1 rw",
        "29 23 0:5 / /proc rw,nosuid shared:7 - proc proc rw",
        "30 23 0:22 / /sys rw,nosuid shared:8 - sysfs sysfs rw",
        "31 23 0:30 / /tmp rw,nosuid shared:9 - tmpfs tmpfs rw",
        "32 23 0:24 / /run rw,nosuid shared:10 - tmpfs run rw",
        "33 32 8:17 / /run/media/me/My\\040Stick rw,nosuid shared:11 - exfat /dev/sdb1 rw",
        "34 23 0:40 / /mnt/c rw,noatime - 9p drvfs rw",
        "35 23 0:25 /@ /mnt/wslg/distro rw,relatime - btrfs /dev/nvme0n1p2 rw",
        "36 23 0:25 /@home/me/projects /srv/projects rw,relatime - btrfs /dev/nvme0n1p2 rw",
    ];

    [Fact]
    public void Reads_mountinfo_and_unescapes_spaces()
    {
        var mounts = LinuxMounts.Parse(MountInfo);

        Assert.Equal(MountInfo.Length, mounts.Count);
        var stick = mounts.Single(m => m.FsType == "exfat");
        Assert.Equal("/run/media/me/My Stick", stick.Target);
        Assert.Equal("/@home", mounts.Single(m => m.Target == "/home").Root);
    }

    [Fact]
    public void Lists_one_drive_per_disk()
    {
        var drives = LinuxMounts.Drives(LinuxMounts.Parse(MountInfo));

        // The btrfs subvolumes are one disk, shown as "/". No /boot, /proc, tmpfs or Windows drives.
        Assert.Equal(["/", "/run/media/me/My Stick"], drives.Select(d => d.Target));
    }

    [Fact]
    public void A_scan_of_root_stays_on_its_disk_but_keeps_subvolumes()
    {
        var skip = LinuxMounts.SkipFor("/", LinuxMounts.Parse(MountInfo));

        // Same disk, different subvolume: part of this drive.
        Assert.False(skip("/home"));
        Assert.False(skip("/var/log"));
        Assert.False(skip("/var/cache/pacman/pkg"));
        // Other file systems take no space here.
        Assert.True(skip("/proc"));
        Assert.True(skip("/tmp"));
        Assert.True(skip("/boot"));
        Assert.True(skip("/mnt/c"));
        Assert.True(skip("/run"));
        // Second views of folders already counted, and snapshots, would count files twice.
        Assert.True(skip("/mnt/wslg/distro"));
        Assert.True(skip("/srv/projects"));
        Assert.True(skip("/.snapshots"));
        // Ordinary folders.
        Assert.False(skip("/usr"));
        Assert.False(skip("/home/me"));
    }

    [Theory]
    [InlineData("XDG_DOWNLOAD_DIR=\"$HOME/Downloads\"", "DOWNLOAD", "/home/me/Downloads")]
    [InlineData("XDG_MUSIC_DIR=\"/data/Music/\"", "MUSIC", "/data/Music")]
    [InlineData("  XDG_DESKTOP_DIR=\"$HOME/Työpöytä\"", "DESKTOP", "/home/me/Työpöytä")]
    public void Reads_user_dirs(string line, string name, string path)
    {
        Assert.Equal((name, path), UserDirs.Parse(line, "/home/me"));
    }

    [Theory]
    [InlineData("# This file is written by xdg-user-dirs-update")]
    [InlineData("XDG_TEMPLATES_DIR=\"$HOME/\"")]
    [InlineData("XDG_PUBLICSHARE_DIR=\"relative\"")]
    [InlineData("")]
    public void Skips_comments_and_odd_user_dirs(string line)
    {
        var parsed = UserDirs.Parse(line, "/home/me");
        Assert.True(parsed is null || parsed.Value.Path == "/home/me");
    }

    [Fact]
    public void Linux_layout_knows_the_xdg_folders()
    {
        var paths = SystemPaths.LinuxUnderRoot(Path.Combine(Path.GetTempPath(), "fake-root"));

        Assert.True(paths.IsLinux);
        Assert.EndsWith(Path.Combine("home", "me", ".cache"), paths.Cache);
        Assert.EndsWith(Path.Combine("home", "me", ".local", "share", "Trash"), paths.Trash);
        Assert.Contains(paths.SystemFolders, f => f.EndsWith(Path.DirectorySeparatorChar + "usr"));
        Assert.Contains(paths.SystemFolders, f => f.EndsWith(Path.DirectorySeparatorChar + "var"));
        Assert.DoesNotContain(paths.SystemFolders, f => f.EndsWith(Path.DirectorySeparatorChar + "home"));
        Assert.Contains(paths.AppDataFolders, f => f.EndsWith(Path.Combine("home", "me", ".local")));
    }

    [Fact]
    public void Journal_usage_is_read_in_binary_units()
    {
        Assert.Equal((long)Math.Round(1.2 * 1024 * 1024 * 1024),
            Ebb.Core.Detectors.JournalDetector.ParseUsage("Archived and active journals take up 1.2G in the file system."));
        Assert.Equal(512L * 1024 * 1024,
            Ebb.Core.Detectors.JournalDetector.ParseUsage("Archived and active journals take up 512.0M in the file system."));
        Assert.Null(Ebb.Core.Detectors.JournalDetector.ParseUsage("No journal files were found."));
    }
}
