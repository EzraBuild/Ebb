using Ebb.Core.Cleanup;
using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.Cleanup;

public class CleanupPlannerTests : IDisposable
{
    private readonly FakeLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    private CleanupPlanner Planner(params string[] installedTools) =>
        new(new PathGuard(_layout.Paths, _layout.Sandbox.Root), new FakeTools(installedTools));

    [Fact]
    public void Remove_step_keeps_default_ticks_and_blocks_protected_items()
    {
        var old = _layout.File(@"Users\me\Downloads\old.exe", 100);
        var keep = _layout.File(@"Users\me\Downloads\keep.exe", 200);
        var system = _layout.File(@"Windows\Temp\x.tmp", 300);
        var finding = Finding("f", new RemoveItems(), Item(old), Item(keep, selected: false), Item(system));

        var step = Assert.Single(Planner().Plan([finding]).Steps);

        Assert.Equal(StepKind.RemoveItems, step.Kind);
        Assert.Equal([old], step.SelectedItems.Select(i => i.Path));
        Assert.NotNull(step.Items.Single(i => i.Path == system).Blocked);
        Assert.Equal(100, step.EstimatedBytes);

        step.Items.Single(i => i.Path == keep).Selected = true;
        step.Items.Single(i => i.Path == system).Selected = true; // can't tick a blocked item
        Assert.Equal(300, step.EstimatedBytes);
    }

    [Fact]
    public void Uses_the_tools_own_command_when_installed()
    {
        var cache = _layout.Dir(@"Users\me\AppData\Local\npm-cache\_cacache");
        var finding = Finding("npm", new RunTool("npm", ["cache", "clean", "--force"]), Item(cache));

        var step = Assert.Single(Planner("npm").Plan([finding]).Steps);

        Assert.Equal(StepKind.RunTool, step.Kind);
        Assert.Equal("npm cache clean --force", step.CommandLine);
        Assert.Empty(step.Items);
        Assert.True(step.IsPermanent(RemovalMode.RecycleBin));
    }

    [Fact]
    public void Falls_back_to_removing_cache_folders_when_the_tool_is_missing()
    {
        var cache = _layout.Dir(@"Users\me\AppData\Local\npm-cache\_cacache");
        var withItems = Finding("npm", new RunTool("npm", ["cache", "clean"]), Item(cache));
        var withoutItems = Finding("docker", new RunTool("docker", ["system", "prune", "-f"]));

        var plan = Planner().Plan([withItems, withoutItems]);

        var step = Assert.Single(plan.Steps);
        Assert.Equal(StepKind.RemoveItems, step.Kind);
        Assert.Contains("npm isn't installed", step.Description);
        Assert.Equal("docker isn't installed.", Assert.Single(plan.Skipped).Reason);
    }

    [Fact]
    public void Admin_steps_show_the_exact_windows_command()
    {
        var plan = Planner().Plan([
            Finding("h", new RunElevated(HelperOperation.HibernateOff)),
            Finding("d", new RunElevated(HelperOperation.ComponentCleanup)),
        ]);

        Assert.All(plan.Steps, s => Assert.True(s.NeedsAdmin));
        Assert.Equal(["powercfg /hibernate off", "DISM /Online /Cleanup-Image /StartComponentCleanup"],
            plan.Steps.Select(s => s.CommandLine));
        Assert.True(plan.NeedsAdmin);
    }

    [Fact]
    public void Shrinking_deletes_nothing_and_warns_about_wsl_shutdown()
    {
        var plan = Planner().Plan([Finding("d", new CompactDisk(@"C:\x\docker_data.vhdx", DiskOwner.Docker, null))]);

        var step = Assert.Single(plan.Steps);
        Assert.False(step.IsPermanent(RemovalMode.Permanent));
        Assert.Contains("Quit Docker Desktop first.", step.Warnings);
        Assert.StartsWith("wsl --shutdown", step.CommandLine);
    }

    [Fact]
    public void Open_only_and_info_findings_are_skipped()
    {
        var plan = Planner().Plan([
            Finding("open", new OpenTarget("cleanmgr.exe", null, "Open Disk Cleanup")),
            Finding("info", null),
        ]);

        Assert.Empty(plan.Steps);
        Assert.Equal(2, plan.Skipped.Count);
    }

    [Fact]
    public void Removing_to_the_recycle_bin_can_be_undone_but_emptying_it_cannot()
    {
        var file = _layout.File(@"Users\me\Downloads\a.zip", 10);
        var plan = Planner().Plan([Finding("r", new RemoveItems(), Item(file)), Finding("b", new EmptyRecycleBin(@"C:\"))]);

        Assert.False(plan.Steps[0].IsPermanent(RemovalMode.RecycleBin));
        Assert.True(plan.Steps[0].IsPermanent(RemovalMode.Permanent));
        Assert.True(plan.Steps[1].IsPermanent(RemovalMode.RecycleBin));
    }

    [Fact]
    public void Things_a_tool_brings_back_skip_the_recycle_bin()
    {
        var folder = _layout.Dir(@"src\app\node_modules");
        var finding = Finding("nm", new RemoveItems("Running npm install"),
            new FindingItem(folder, 20L * 1024 * 1024 * 1024, DateTime.UtcNow, SelectedByDefault: true));

        var step = Assert.Single(Planner().Plan([finding]).Steps);

        Assert.True(step.SkipsRecycleBin);
        Assert.True(step.IsPermanent(RemovalMode.RecycleBin));
        Assert.Equal(RemovalMode.Permanent, step.RemovalFor(RemovalMode.RecycleBin));
        Assert.Contains("Running npm install brings them back", step.Description);
        Assert.Empty(step.Warnings); // no "too big for the Recycle Bin" warning, it doesn't go there
    }

    internal static FindingItem Item(string path, bool selected = true) =>
        new(path, File.Exists(path) ? new FileInfo(path).Length : 0, DateTime.UtcNow, SelectedByDefault: selected);

    internal static Finding Finding(string id, FindingAction? action, params FindingItem[] items) => new()
    {
        Id = id,
        Category = Category.Developer,
        Title = id,
        Reason = "",
        Safety = Safety.Safe,
        Bytes = items.Sum(i => i.Bytes),
        Items = items,
        Action = action,
    };
}
