using Ebb.Core.Cleanup;
using Ebb.Core.Detectors;
using Ebb.Core.Findings;
using Ebb.Core.Scanning;
using Ebb.Linux.Services;
using Ebb.Linux.ViewModels;
using Ebb.Tests;
using static Ebb.Linux.Tests.LinuxLayout;

namespace Ebb.Linux.Tests;

public class LinuxViewModelTests : IDisposable
{
    private readonly LinuxLayout _layout = new();
    private readonly RecordingRemover _remover = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly AppServices _services;

    public LinuxViewModelTests()
    {
        var guard = new PathGuard(_layout.Paths, _layout.Sandbox.Root);
        var log = new CleanupLog(_layout.PathOf("log/cleanup-log.jsonl"));
        _services = new AppServices
        {
            Paths = _layout.Paths,
            Scanner = _ => new DriveScanner(new ScanOptions { PruneBelow = 0, LargeFileThreshold = MB }),
            Engine = new FindingEngine([new PackageCacheDetector(), new UserCacheDetector(), new DownloadsDetector()]),
            Planner = new CleanupPlanner(guard, new FakeTools()),
            Executor = new CleanupExecutor(guard, _remover, new FakeRunner(), new FakeRecycleBin(), new FakeElevated(), new FakeCompactor(), log),
            Log = log,
        };

        _layout.File("home/me/.npm/_cacache/content-v2/blob", 3 * MB);                    // developer, safe
        _layout.File("home/me/.cache/thumbnails/large/a.png", 2 * MB);                    // system, safe
        Directory.SetLastWriteTimeUtc(_layout.PathOf("home/me/.cache/thumbnails"), _layout.DaysAgo(5));
        _layout.File("home/me/Downloads/Obsidian.AppImage", 4 * MB, _layout.DaysAgo(90)); // personal, check first
    }

    public void Dispose() => _layout.Dispose();

    private MainViewModel Main() =>
        new(_services, _dialogs, [new DriveViewModel("/", 100 * MB, 10 * MB), new DriveViewModel(_layout.Sandbox.Root, 100 * MB, 10 * MB)]);

    [Fact]
    public async Task Scan_fills_each_page_with_linux_findings()
    {
        var main = Main();
        Assert.Equal("Why is my disk full?", main.Heading);
        main.SelectedDrive = main.Drives[1];
        Assert.StartsWith("Why is ", main.Heading);

        await main.ScanCommand.ExecuteAsync(null);

        Assert.True(main.HasResults);
        Assert.Equal("npm cache", Assert.Single(main.Developer.Findings).Title);
        Assert.Equal("App caches", Assert.Single(main.SystemPage.Findings).Title);
        Assert.Equal("Old installers and archives in Downloads", Assert.Single(main.Personal.Findings).Title);
        Assert.Equal("System", main.SystemPage.Title);
        Assert.Equal(5 * MB, main.SafeBytes);
        Assert.Equal("~/.npm/_cacache", main.Developer.Findings[0].Meta.Split(' ').Last());
    }

    [Fact]
    public async Task Review_runs_only_after_confirming_and_says_trash()
    {
        var main = Main();
        main.SelectedDrive = main.Drives[1];
        await main.ScanCommand.ExecuteAsync(null);
        main.Personal.Findings[0].IsSelected = true;
        _dialogs.OnShow = async cleanup =>
        {
            Assert.True(cleanup.IsReview);
            Assert.Empty(_remover.Removed); // nothing happens before the user confirms
            Assert.Contains("go to the Trash", cleanup.Steps[0].ItemsSummary);
            await cleanup.CleanCommand.ExecuteAsync(null);
        };

        await main.ReviewCleanupCommand.ExecuteAsync(null);

        var removed = Assert.Single(_remover.Removed);
        Assert.Equal(RemovalMode.RecycleBin, removed.Mode);
        Assert.Empty(main.Personal.Findings); // done findings disappear
        Assert.Contains("to the Trash", main.Notice);
    }

    [Fact]
    public async Task Caches_skip_the_trash_and_say_so()
    {
        var main = Main();
        main.SelectedDrive = main.Drives[1];
        await main.ScanCommand.ExecuteAsync(null);
        var appCaches = main.SystemPage.Findings[0];

        Assert.Contains("skipping the Trash", appCaches.ActionText);
        Assert.True(appCaches.IsSafe);
    }

    [Fact]
    public void Review_window_filters_by_folder_and_unticks_only_what_it_shows()
    {
        var shop = _layout.Dir("home/me/code/shop/node_modules");
        var blog = _layout.Dir("home/me/code/blog/node_modules");
        var other = _layout.Dir("home/me/work/api/node_modules");
        var more = _layout.Dir("home/me/work/web/node_modules");
        var finding = new Finding
        {
            Id = "dev.node-modules", Category = Category.Developer, Title = "node_modules", Reason = "", Safety = Safety.Safe,
            Bytes = 4 * MB, Action = new RemoveItems("npm install"),
            Items = [.. new[] { shop, blog, other, more }.Select(p => new FindingItem(p, MB, DateTime.UtcNow))],
        };
        var cleanup = new CleanupViewModel(_services.Planner.Plan([finding]), _services.Executor, "log", _dialogs);
        var step = Assert.Single(cleanup.Steps);

        step.Filter = "code";
        step.UntickAllCommand.Execute(null);

        Assert.Equal([false, false, true, true], step.Items.Select(i => i.Selected));
        Assert.Contains("deleted for good", step.ItemsSummary);
        Assert.True(step.IsPermanent);
    }

    [Fact]
    public void Review_window_says_which_page_each_step_came_from()
    {
        PlanStep Step(string id, Category category) => new()
        {
            Finding = new Finding
            {
                Id = id, Category = category, Title = id, Reason = "", Safety = Safety.Safe, Bytes = MB, Action = new RemoveItems(),
            },
            Kind = StepKind.RemoveItems,
            Description = "",
        };
        var plan = new CleanupPlan([Step("a", Category.Developer), Step("b", Category.Windows), Step("c", Category.Windows)], []);

        var cleanup = new CleanupViewModel(plan, _services.Executor, "log", _dialogs);

        Assert.Equal(["DEVELOPER FILES", "SYSTEM", null], cleanup.Steps.Select(s => s.Group));
    }

    private sealed class FakeDialogs : IDialogs
    {
        public Func<CleanupViewModel, Task>? OnShow { get; set; }

        public Task ShowCleanupAsync(CleanupViewModel cleanup) => OnShow?.Invoke(cleanup) ?? Task.CompletedTask;

        public void Open(string target, string? arguments = null)
        {
        }

        public void Reveal(string path)
        {
        }
    }
}
