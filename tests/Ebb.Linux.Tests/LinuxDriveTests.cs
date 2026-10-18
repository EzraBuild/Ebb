using Ebb.Core;
using Ebb.Core.Cleanup;
using Ebb.Core.Detectors;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Tests;
using static Ebb.Linux.Tests.LinuxLayout;

namespace Ebb.Linux.Tests;

public class LinuxDriveTests : IDisposable
{
    private readonly LinuxLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    /// <summary>A Trash that thinks run/media/me/Stick is another disk.</summary>
    private FreedesktopTrash TrashWithStick()
    {
        var stick = _layout.PathOf("run/media/me/Stick");
        return new FreedesktopTrash(_layout.Paths.Trash,
            path => PathUtil.IsSameOrUnder(path, stick)
                ? new Mount("8:17", "/", stick, "exfat", "/dev/sdb1")
                : new Mount("0:25", "/", _layout.Sandbox.Root, "ext4", "/dev/nvme0n1p2"),
            "1000");
    }

    [Fact]
    public void Things_on_another_drive_go_to_that_drives_own_trash()
    {
        var trash = TrashWithStick();
        var photo = _layout.File("run/media/me/Stick/photos/a b.jpg", 7);
        var stick = _layout.PathOf("run/media/me/Stick");

        trash.MoveToTrash(photo);

        var own = Path.Combine(stick, ".Trash-1000");
        Assert.True(File.Exists(Path.Combine(own, "files", "a b.jpg")));
        var note = File.ReadAllText(Path.Combine(own, "info", "a b.jpg.trashinfo"));
        Assert.Contains("Path=photos/a%20b.jpg\n", note); // relative to the drive, so it survives a new mount point
        Assert.False(Directory.Exists(Path.Combine(_layout.Paths.Trash, "files")));

        Assert.Equal(1, trash.Query(own).Items);
        trash.Empty(own);
        Assert.Equal(0, trash.Query(own).Items);
    }

    [Fact]
    public void A_shared_trash_with_the_sticky_bit_is_used_on_linux()
    {
        if (OperatingSystem.IsWindows())
            return; // sticky bits only exist on Linux
        var trash = TrashWithStick();
        var shared = _layout.Dir("run/media/me/Stick/.Trash");
        File.SetUnixFileMode(shared, (UnixFileMode)0b1_111_111_111); // 1777, like /tmp
        var file = _layout.File("run/media/me/Stick/notes.txt", 3);

        trash.MoveToTrash(file);

        Assert.True(File.Exists(Path.Combine(shared, "1000", "files", "notes.txt")));
        Assert.False(Directory.Exists(_layout.PathOf("run/media/me/Stick/.Trash-1000")));
    }

    [Fact]
    public async Task The_trash_on_a_scanned_drive_shows_up_with_the_home_one()
    {
        _layout.File(".Trash-1000/files/old.iso", 4 * MB);
        _layout.File(".Trash-1000/info/old.iso.trashinfo", 10);
        _layout.File("home/me/.local/share/Trash/files/old.txt", 2 * MB);
        var trash = TrashWithStick();

        var findings = await Run(new TrashDetector(trash, uid: "1000"), _layout.Context());

        Assert.Equal(["Trash", "Trash on this drive"], findings.Select(f => f.Title));
        Assert.Equal(4 * MB, findings[1].Bytes);
        Assert.Equal(new EmptyRecycleBin(_layout.PathOf(".Trash-1000"), IsTrash: true), findings[1].Action);
    }

    [Fact]
    public void Guard_allows_your_usb_drive_but_not_its_root_or_its_trash()
    {
        var guard = new PathGuard(_layout.Paths, _layout.Sandbox.Root);
        Assert.Null(guard.Check(_layout.File("run/media/me/Stick/video.mp4", 1)));
        Assert.NotNull(guard.Check(_layout.Dir("run/media/me/Stick")));
        Assert.Contains("Trash", guard.Check(_layout.File("run/media/me/Stick/.Trash-1000/files/x", 1)));
        Assert.NotNull(guard.Check(_layout.File("run/user/1000/thing", 1)));
    }
}
