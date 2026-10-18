using Ebb.App.Services;
using Ebb.App.ViewModels;
using Ebb.Core.Cleanup;
using Ebb.Core.Detectors;
using Ebb.Core.Findings;
using Ebb.Core.Scanning;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.App;

public class ViewModelTests : IDisposable
{
    private readonly FakeLayout _layout = new();
    private readonly RecordingRemover _remover = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly AppServices _services;

    public ViewModelTests()
    {
        var guard = new PathGuard(_layout.Paths, _layout.Sandbox.Root);
        var log = new CleanupLog(_layout.PathOf(@"log\cleanup-log.jsonl"));
        _services = new AppServices
        {
            Paths = _layout.Paths,
            Scanner = new DriveScanner(new ScanOptions { PruneBelow = 0, LargeFileThreshold = MB }),
            Engine = new FindingEngine([new PackageCacheDetector(), new TempFilesDetector(), new DownloadsDetector()]),
            Planner = new CleanupPlanner(guard, new FakeTools()),
            Executor = new CleanupExecutor(guard, _remover, new FakeRunner(), new FakeRecycleBin(), new FakeElevated(), new FakeCompactor(), log),
            Log = log,
        };

        _layout.File(@"Users\me\AppData\Local\npm-cache\_cacache\blob", 3 * MB);              // developer, safe
        _layout.File(@"Users\me\AppData\Local\Temp\old.tmp", 2 * MB, _layout.DaysAgo(5));    // windows, safe
        _layout.File(@"Users\me\Downloads\setup.exe", 4 * MB, _layout.DaysAgo(90));         // personal, check first
    }

    public void Dispose() => _layout.Dispose();

    private MainViewModel Main() =>
        new(_services, _dialogs, [new DriveViewModel(_layout.Sandbox.Root, 100 * MB, 10 * MB)]);

    [Fact]
    public async Task Scan_fills_each_page_and_counts_what_is_safe()
    {
        var main = Main();
        Assert.True(main.IsEmpty);

        await main.ScanCommand.ExecuteAsync(null);

        Assert.True(main.HasResults);
        Assert.Equal("npm cache", Assert.Single(main.Developer.Findings).Title);
        Assert.Equal("Temporary files", Assert.Single(main.Windows.Findings).Title);
        Assert.Equal("Old installers and archives in Downloads", Assert.Single(main.Personal.Findings).Title);
        Assert.Equal(5 * MB, main.SafeBytes);
        Assert.NotEmpty(main.Folders.Rows);
        Assert.StartsWith("Scanned 3 files", main.Notice);
    }

    [Fact]
    public async Task Ticking_updates_the_footer_and_tick_safe_skips_check_first()
    {
        var main = Main();
        await main.ScanCommand.ExecuteAsync(null);
        Assert.False(main.ReviewCleanupCommand.CanExecute(null));

        main.SelectSafeCommand.Execute(null);

        Assert.Equal(2, main.SelectedCount);
        Assert.Equal(5 * MB, main.SelectedBytes);
        Assert.False(main.Personal.Findings[0].IsSelected);
        Assert.True(main.ReviewCleanupCommand.CanExecute(null));
        Assert.Contains("2 items ticked", main.SelectionText);
    }

    [Fact]
    public async Task Review_shows_the_plan_and_only_runs_after_confirming()
    {
        var main = Main();
        await main.ScanCommand.ExecuteAsync(null);
        main.Windows.Findings[0].IsSelected = true;
        _dialogs.OnShow = cleanup =>
        {
            Assert.True(cleanup.IsReview);
            Assert.Empty(_remover.Removed); // nothing happens before the user confirms
            cleanup.CleanCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        };

        main.ReviewCleanupCommand.Execute(null);

        var removed = Assert.Single(_remover.Removed);
        Assert.Equal((_layout.PathOf(@"Users\me\AppData\Local\Temp\old.tmp"), RemovalMode.RecycleBin), removed);
        Assert.Empty(main.Windows.Findings); // done findings disappear
        Assert.Contains("Recycle Bin", main.Notice);
        Assert.Equal(0, main.SelectedCount);
    }

    [Fact]
    public async Task Closing_the_review_without_confirming_changes_nothing()
    {
        var main = Main();
        await main.ScanCommand.ExecuteAsync(null);
        main.SelectSafeCommand.Execute(null);
        _dialogs.OnShow = cleanup => cleanup.CloseCommand.Execute(null);

        main.ReviewCleanupCommand.Execute(null);

        Assert.Empty(_remover.Removed);
        Assert.Equal(2, main.SelectedCount);
    }

    [Fact]
    public void Review_window_follows_ticks_and_the_recycle_bin_choice()
    {
        var a = _layout.File(@"Users\me\Downloads\a.zip", 2 * MB);
        var b = _layout.File(@"Users\me\Downloads\b.zip", 3 * MB);
        var finding = Cleanup.CleanupPlannerTests.Finding("f", new RemoveItems(),
            Cleanup.CleanupPlannerTests.Item(a), Cleanup.CleanupPlannerTests.Item(b));
        var cleanup = new CleanupViewModel(_services.Planner.Plan([finding]), _services.Executor, "log", _dialogs);
        var step = Assert.Single(cleanup.Steps);

        Assert.Equal("Clean up 5 MB", cleanup.ConfirmText);
        Assert.False(step.IsPermanent);

        cleanup.UseRecycleBin = false;
        Assert.True(step.IsPermanent);
        Assert.Equal(RemovalMode.Permanent, cleanup.Plan.RemovalMode);

        step.Items[1].Selected = false;
        Assert.Equal("Clean up 2 MB", cleanup.ConfirmText);

        step.UntickAllCommand.Execute(null);
        Assert.False(cleanup.CleanCommand.CanExecute(null));
    }

    [Fact]
    public void Review_window_can_untick_a_whole_folder()
    {
        var count = _layout.Dir(@"Users\me\Desktop\Main\count\node_modules");
        var other = _layout.Dir(@"Users\me\Desktop\Main\other\node_modules");
        var one = _layout.Dir(@"Users\me\Documents\one\node_modules");
        var two = _layout.Dir(@"Users\me\Documents\two\node_modules");
        var finding = Cleanup.CleanupPlannerTests.Finding("nm", new RemoveItems("npm install"),
            [.. new[] { count, other, one, two }.Select(p => new FindingItem(p, MB, DateTime.UtcNow, SelectedByDefault: true))]);
        var cleanup = new CleanupViewModel(_services.Planner.Plan([finding]), _services.Executor, "log", _dialogs);

        // "Desktop" holds the same two as "Desktop\Main", and the profile holds everything: both left out.
        Assert.Equal([_layout.PathOf(@"Users\me\Desktop\Main"), _layout.PathOf(@"Users\me\Documents")],
            cleanup.Folders.Select(f => f.Path));
        var desktop = cleanup.Folders[0];
        var documents = cleanup.Folders[1];
        Assert.Equal("2 items · 2 MB", desktop.Summary);

        desktop.Selected = false;
        Assert.Equal([false, false, true, true], cleanup.Steps[0].Items.Select(i => i.Selected));
        Assert.Equal("Clean up 2 MB", cleanup.ConfirmText);

        cleanup.Steps[0].Items[2].Selected = false;
        Assert.Null(documents.Selected); // some ticked

        documents.Selected = true;
        Assert.True(cleanup.Steps[0].Items[2].Selected);
        Assert.False(desktop.Selected);
    }

    [Fact]
    public async Task Folder_browser_goes_in_and_back_up()
    {
        var main = Main();
        await main.ScanCommand.ExecuteAsync(null);
        var folders = main.Folders;
        Assert.False(folders.UpCommand.CanExecute(null));

        folders.Rows.Single(r => r.Name == "Users").OpenCommand.Execute(null);
        Assert.EndsWith("Users", folders.CurrentPath);
        Assert.True(folders.UpCommand.CanExecute(null));

        folders.UpCommand.Execute(null);
        Assert.Equal(_layout.Sandbox.Root, folders.CurrentPath);
    }

    private sealed class FakeDialogs : IDialogs
    {
        public Action<CleanupViewModel>? OnShow { get; set; }

        public void ShowCleanup(CleanupViewModel cleanup) => OnShow?.Invoke(cleanup);

        public void Open(string target, string? arguments = null)
        {
        }

        public void Reveal(string path)
        {
        }
    }
}
