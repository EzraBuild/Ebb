using Ebb.Core.Cleanup;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Tests;
using static Ebb.Linux.Tests.LinuxLayout;

namespace Ebb.Linux.Tests;

public class LinuxCleanupTests : IDisposable
{
    private readonly LinuxLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    private PathGuard Guard() => new(_layout.Paths, _layout.Sandbox.Root);

    [Fact]
    public void Guard_refuses_system_folders_main_folders_the_trash_and_other_users()
    {
        var guard = Guard();
        string[] refused =
        [
            _layout.File("usr/lib/node_modules/x/index.js", 1),
            _layout.File("var/cache/pacman/pkg/linux-6.9.pkg.tar.zst", 1),
            _layout.File("etc/fstab", 1),
            _layout.Dir("home/me"),
            _layout.Dir("home/me/.cache"),
            _layout.Dir("home/me/.config"),
            _layout.Dir("home/me/.local"),
            _layout.Dir("home/me/.local/share"),
            _layout.Dir("home/me/Downloads"),
            _layout.File("home/me/.local/share/Trash/files/old.txt", 1),
            _layout.File("home/other/notes.txt", 1),
            _layout.Dir("tmp"),
        ];
        foreach (var path in refused)
            Assert.NotNull(guard.Check(path));

        Assert.Contains("System", guard.Check(_layout.PathOf("usr/lib/node_modules/x/index.js")));
        Assert.Contains("Trash", guard.Check(_layout.PathOf("home/me/.local/share/Trash/files/old.txt")));
        Assert.Contains("another user", guard.Check(_layout.PathOf("home/other/notes.txt")));
    }

    [Fact]
    public void Guard_allows_things_inside_your_folders()
    {
        var guard = Guard();
        Assert.Null(guard.Check(_layout.Dir("home/me/code/app/node_modules")));
        Assert.Null(guard.Check(_layout.Dir("home/me/.cache/mozilla")));
        Assert.Null(guard.Check(_layout.File("home/me/Downloads/app.AppImage", 1)));
    }

    [Fact]
    public void Trash_keeps_where_each_item_came_from_and_never_overwrites()
    {
        var trash = new FreedesktopTrash(_layout.Paths.Trash);
        var first = _layout.File("home/me/Downloads/a b.zip", 10);
        var folder = _layout.Dir("home/me/code/old");
        _layout.File("home/me/code/old/file.txt", 5);

        trash.MoveToTrash(first, new DateTime(2026, 10, 18, 21, 5, 7));
        trash.MoveToTrash(folder);
        var second = _layout.File("home/me/Downloads/a b.zip", 20);
        trash.MoveToTrash(second);

        var files = Path.Combine(_layout.Paths.Trash, "files");
        var info = Path.Combine(_layout.Paths.Trash, "info");
        Assert.False(File.Exists(first));
        Assert.False(Directory.Exists(folder));
        Assert.Equal(10, new FileInfo(Path.Combine(files, "a b.zip")).Length);
        Assert.Equal(20, new FileInfo(Path.Combine(files, "a b.zip.2")).Length);
        Assert.True(File.Exists(Path.Combine(files, "old", "file.txt")));

        var note = File.ReadAllText(Path.Combine(info, "a b.zip.trashinfo"));
        Assert.StartsWith("[Trash Info]\n", note);
        Assert.Contains("a%20b.zip\n", note);
        Assert.Contains("DeletionDate=2026-10-18T21:05:07\n", note);
        Assert.True(File.Exists(Path.Combine(info, "a b.zip.2.trashinfo")));

        var bin = trash.Query("");
        Assert.Equal(3, bin.Items);
        Assert.Equal(35, bin.Bytes);

        trash.Empty("");
        Assert.Empty(Directory.EnumerateFileSystemEntries(files));
        Assert.Empty(Directory.EnumerateFileSystemEntries(info));
    }

    [Fact]
    public void Trash_paths_are_url_escaped_per_folder()
    {
        Assert.Equal("/home/me/My%20Files/caf%C3%A9.txt", FreedesktopTrash.EncodePath("/home/me/My Files/café.txt"));
    }

    [Fact]
    public void Linux_remover_trashes_or_deletes_for_good()
    {
        var trash = new FreedesktopTrash(_layout.Paths.Trash);
        var remover = new LinuxFileRemover(trash);
        var toTrash = _layout.File("home/me/Videos/clip.mp4", 3);
        var toDelete = _layout.Dir("home/me/code/app/node_modules");
        _layout.File("home/me/code/app/node_modules/x/index.js", 3);

        remover.Remove(toTrash, RemovalMode.RecycleBin);
        remover.Remove(toDelete, RemovalMode.Permanent);

        Assert.True(File.Exists(Path.Combine(_layout.Paths.Trash, "files", "clip.mp4")));
        Assert.False(Directory.Exists(toDelete));
        Assert.False(Directory.Exists(Path.Combine(_layout.Paths.Trash, "files", "node_modules")));
    }

    [Fact]
    public void Root_tools_run_through_pkexec_and_say_so_in_the_plan()
    {
        var finding = new Finding
        {
            Id = "linux.cache.pacman", Category = Category.Windows, Title = "pacman package cache", Reason = "",
            Safety = Safety.Safe, Bytes = 5 * MB, Path = _layout.Dir("var/cache/pacman/pkg"),
            Action = new RunTool("paccache", ["-rk1"], AsRoot: true),
        };

        var plan = new CleanupPlanner(Guard(), new FakeTools("paccache", "pkexec")).Plan([finding]);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("pkexec paccache -rk1", step.CommandLine);
        Assert.True(step.NeedsAdmin);
        Assert.Contains("password", step.Description);

        var noPolkit = new CleanupPlanner(Guard(), new FakeTools("paccache")).Plan([finding]);
        Assert.Empty(noPolkit.Steps);
        Assert.Contains("sudo paccache -rk1", Assert.Single(noPolkit.Skipped).Reason);
    }

    [Fact]
    public async Task Root_tools_ask_for_the_password_and_handle_a_closed_prompt()
    {
        var finding = new Finding
        {
            Id = "linux.journal", Category = Category.Windows, Title = "System logs", Reason = "",
            Safety = Safety.Safe, Bytes = 400 * MB, Path = _layout.Dir("var/log/journal"),
            Action = new RunTool("journalctl", ["--vacuum-size=100M"], AsRoot: true),
        };
        var plan = new CleanupPlanner(Guard(), new FakeTools("journalctl", "pkexec")).Plan([finding]);
        var log = new CleanupLog(_layout.PathOf("log/cleanup-log.jsonl"));

        var ran = new FakeRunner();
        var report = await Executor(ran, log).ExecuteAsync(plan);
        Assert.Matches(@"pkexec .*journalctl\.exe --vacuum-size=100M$", Assert.Single(ran.Calls));
        Assert.Equal(StepOutcome.Done, Assert.Single(report.Results).Outcome);

        var closed = new FakeRunner((_, _) => new CommandResult(126, "", "Error executing command as another user: Request dismissed"));
        var refused = await Executor(closed, log).ExecuteAsync(plan);
        var result = Assert.Single(refused.Results);
        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Contains("password prompt", result.Message);
    }

    [Fact]
    public void Plans_on_linux_say_trash_not_recycle_bin()
    {
        var finding = new Finding
        {
            Id = "linux.cache.apps", Category = Category.Windows, Title = "App caches", Reason = "", Safety = Safety.Safe,
            Bytes = MB, Action = new RemoveItems("The app that made each one"),
            Items = [new FindingItem(_layout.Dir("home/me/.cache/mozilla"), MB, DateTime.UtcNow)],
        };

        var step = Assert.Single(new CleanupPlanner(Guard(), new FakeTools()).Plan([finding]).Steps);

        Assert.Contains(OperatingSystem.IsWindows() ? "skip the Recycle Bin" : "skip the Trash", step.Description);
        Assert.Empty(step.Warnings);
    }

    [Fact]
    public async Task Emptying_the_trash_says_trash()
    {
        var finding = new Finding
        {
            Id = "linux.trash", Category = Category.Windows, Title = "Trash", Reason = "", Safety = Safety.CheckFirst,
            Bytes = 3 * MB, Path = _layout.Paths.Trash, Action = new EmptyRecycleBin(_layout.Paths.Trash, IsTrash: true),
        };
        var plan = new CleanupPlanner(Guard(), new FakeTools()).Plan([finding]);
        Assert.StartsWith("Empties the Trash.", Assert.Single(plan.Steps).Description);

        var bin = new FakeRecycleBin();
        var report = await new CleanupExecutor(Guard(), new RecordingRemover(), new FakeRunner(), bin, new FakeElevated(),
            new FakeCompactor(), new CleanupLog(_layout.PathOf("log/cleanup-log.jsonl"))).ExecuteAsync(plan);

        Assert.Equal([_layout.Paths.Trash], bin.Emptied);
        Assert.StartsWith("Emptied the Trash", Assert.Single(report.Results).Message);
    }

    private CleanupExecutor Executor(ICommandRunner runner, CleanupLog log) =>
        new(Guard(), new RecordingRemover(), runner, new FakeRecycleBin(), new FakeElevated(), new FakeCompactor(), log);
}
