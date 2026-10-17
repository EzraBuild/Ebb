using Ebb.Core.Cleanup;
using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Core.Vhdx;
using static Ebb.Tests.Cleanup.CleanupPlannerTests;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.Cleanup;

public class CleanupExecutorTests : IDisposable
{
    private readonly FakeLayout _layout = new();
    private readonly PathGuard _guard;
    private readonly CleanupLog _log;

    public CleanupExecutorTests()
    {
        _guard = new PathGuard(_layout.Paths, _layout.Sandbox.Root);
        _log = new CleanupLog(_layout.PathOf(@"log\cleanup-log.jsonl"));
    }

    public void Dispose() => _layout.Dispose();

    private CleanupExecutor Executor(IFileRemover? remover = null, ICommandRunner? runner = null,
        IRecycleBin? bin = null, IElevatedRunner? elevated = null, IDiskCompactor? compactor = null) =>
        new(_guard, remover ?? new ShellFileRemover(), runner ?? new FakeRunner(), bin ?? new FakeRecycleBin(),
            elevated ?? new FakeElevated(), compactor ?? new FakeCompactor(), _log);

    private CleanupPlan Plan(params Finding[] findings) =>
        new CleanupPlanner(_guard, new FakeTools("npm", "docker")).Plan(findings);

    [Fact]
    public async Task Deletes_only_ticked_items_and_logs_each_one()
    {
        var gone = _layout.File(@"Users\me\Downloads\old.exe", MB);
        var folder = _layout.File(@"src\app\node_modules\pkg\index.js", MB);
        var kept = _layout.File(@"Users\me\Downloads\keep.exe", MB);
        var plan = Plan(Finding("f", new RemoveItems(), Item(gone), Item(Path.GetDirectoryName(Path.GetDirectoryName(folder))!), Item(kept, selected: false)));
        plan.RemovalMode = RemovalMode.Permanent;

        var report = await Executor().ExecuteAsync(plan);

        Assert.False(File.Exists(gone));
        Assert.False(Directory.Exists(_layout.PathOf(@"src\app\node_modules")));
        Assert.True(File.Exists(kept));
        var result = Assert.Single(report.Results);
        Assert.Equal(StepOutcome.Done, result.Outcome);
        Assert.Equal(MB, report.FreedBytes);
        var entries = _log.Read();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.True(e.Succeeded && e.Action == "delete" && e.Finding == "f"));
    }

    [Fact]
    public async Task Uses_the_recycle_bin_by_default()
    {
        var file = _layout.File(@"Users\me\Downloads\a.zip", 5000);
        var remover = new RecordingRemover();
        var plan = Plan(Finding("f", new RemoveItems(), Item(file)));

        var report = await Executor(remover).ExecuteAsync(plan);

        Assert.Equal([(file, RemovalMode.RecycleBin)], remover.Removed);
        Assert.Equal(5000, report.RecycledBytes);
        Assert.Equal(0, report.FreedBytes);
        Assert.Contains("Recycle Bin", report.Results[0].Message);
        Assert.Equal("recycle", Assert.Single(_log.Read()).Action);
    }

    [Fact]
    public async Task Checks_the_guard_again_right_before_removing()
    {
        var system = _layout.File(@"Windows\System32\kernel.dll", 10);
        var step = new PlanStep
        {
            Finding = Finding("evil", new RemoveItems()),
            Kind = StepKind.RemoveItems,
            Description = "",
            Items = [new PlanItem(system, 10, null, selected: true, blocked: null)], // as if the planner was bypassed
        };
        var remover = new RecordingRemover();

        var report = await Executor(remover).ExecuteAsync(new CleanupPlan([step], []) { RemovalMode = RemovalMode.Permanent });

        Assert.True(File.Exists(system));
        Assert.Empty(remover.Removed);
        Assert.Equal(StepOutcome.Failed, report.Results[0].Outcome);
        var entry = Assert.Single(_log.Read());
        Assert.Equal("refuse", entry.Action);
        Assert.False(entry.Succeeded);
    }

    [Fact]
    public async Task Reports_items_that_could_not_be_removed()
    {
        var ok = _layout.File(@"Users\me\AppData\Local\Temp\a.tmp", 10);
        var locked = _layout.File(@"Users\me\AppData\Local\Temp\locked.tmp", 10);
        var remover = new RecordingRemover(path => path == locked ? new IOException("It's in use by another app.") : null);

        var report = await Executor(remover).ExecuteAsync(Plan(Finding("t", new RemoveItems(), Item(ok), Item(locked))));

        var result = report.Results[0];
        Assert.Equal(StepOutcome.Partly, result.Outcome);
        Assert.Contains("locked.tmp: It's in use", result.Message);
        Assert.Equal(2, _log.Read().Count);
    }

    [Fact]
    public async Task Runs_the_tool_and_measures_what_it_freed()
    {
        var cache = _layout.PathOf(@"Users\me\AppData\Local\npm-cache");
        _layout.File(@"Users\me\AppData\Local\npm-cache\_cacache\blob", 2 * MB);
        var runner = new FakeRunner((_, _) =>
        {
            Directory.Delete(Path.Combine(cache, "_cacache"), recursive: true); // what npm would do
            return new CommandResult(0, "done", "");
        });
        var finding = Finding("npm", new RunTool("npm", ["cache", "clean", "--force"]), Item(Path.Combine(cache, "_cacache")));

        var report = await Executor(runner: runner).ExecuteAsync(Plan(finding));

        Assert.Equal(["npm cache clean --force"], runner.Calls);
        Assert.Equal(2 * MB, report.FreedBytes);
        var entry = Assert.Single(_log.Read());
        Assert.Equal(("command", "npm cache clean --force", true), (entry.Action, entry.Target, entry.Succeeded));
    }

    [Fact]
    public async Task Tool_failures_are_reported()
    {
        var runner = new FakeRunner((_, _) => new CommandResult(1, "", "error during connect: is the docker daemon running?"));

        var report = await Executor(runner: runner).ExecuteAsync(Plan(Finding("d", new RunTool("docker", ["system", "prune", "-f"]))));

        Assert.Equal(StepOutcome.Failed, report.Results[0].Outcome);
        Assert.Contains("docker daemon", report.Results[0].Message);
        Assert.False(Assert.Single(_log.Read()).Succeeded);
    }

    [Fact]
    public async Task Hands_admin_bin_and_disk_steps_to_the_right_service()
    {
        var bin = new FakeRecycleBin(5 * MB, 3);
        var elevated = new FakeElevated();
        var compactor = new FakeCompactor();
        var disk = new CompactDisk(@"C:\wsl\ext4.vhdx", DiskOwner.Wsl, "Ubuntu");
        var plan = Plan(
            Finding("bin", new EmptyRecycleBin(@"C:\")) with { Bytes = 5 * MB },
            Finding("hib", new RunElevated(HelperOperation.HibernateOff)),
            Finding("wsl", disk));

        var report = await Executor(bin: bin, elevated: elevated, compactor: compactor).ExecuteAsync(plan);

        Assert.Equal([@"C:\"], bin.Emptied);
        Assert.Equal([new HelperRequest(HelperOperation.HibernateOff)], elevated.Requests);
        Assert.Equal([disk], compactor.Disks);
        Assert.Equal(5 * MB + 1234 + 6000, report.FreedBytes);
        Assert.All(report.Results, r => Assert.Equal(StepOutcome.Done, r.Outcome));
        Assert.Equal(["empty-recycle-bin", "admin-command", "compact"], _log.Read().Select(e => e.Action));
    }

    [Fact]
    public async Task Stopping_skips_the_remaining_steps()
    {
        var file = _layout.File(@"Users\me\Downloads\a.zip", 10);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        var report = await Executor().ExecuteAsync(Plan(Finding("f", new RemoveItems(), Item(file))), null, cancel.Token);

        Assert.True(File.Exists(file));
        Assert.Equal(StepOutcome.Skipped, report.Results[0].Outcome);
    }
}
