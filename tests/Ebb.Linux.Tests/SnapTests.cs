using Ebb.Core.Cleanup;
using Ebb.Core.Detectors;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Tests;
using static Ebb.Linux.Tests.LinuxLayout;

namespace Ebb.Linux.Tests;

public class SnapTests : IDisposable
{
    private readonly LinuxLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    private const string SnapList = """
        Name      Version          Rev    Tracking         Publisher   Notes
        core22    20240111         1122   latest/stable    canonical✓  base,disabled
        core22    20240408         1380   latest/stable    canonical✓  base
        firefox   124.0-2          4033   latest/stable/…  mozilla✓    disabled
        firefox   125.0.1-1        4173   latest/stable/…  mozilla✓    -
        evil;rm   1                9      latest/stable    someone     disabled
        snapd     2.62             21465  latest/stable    canonical✓  snapd
        """;

    [Fact]
    public void Reads_old_snap_versions_and_skips_anything_odd()
    {
        var old = SnapDetector.Disabled(SnapList);

        Assert.Equal([new SnapDetector.OldRevision("core22", "1122"), new SnapDetector.OldRevision("firefox", "4033")], old);
    }

    [Fact]
    public async Task Old_snap_versions_are_removed_with_snap_as_root()
    {
        _layout.File("var/lib/snapd/snaps/core22_1122.snap", 2 * MB);
        _layout.File("var/lib/snapd/snaps/firefox_4033.snap", 3 * MB);
        var runner = new FakeRunner((_, args) => new CommandResult(0, args is ["list", "--all"] ? SnapList : "", ""));

        var finding = Assert.Single(await Run(new SnapDetector(runner, new FakeTools("snap")), _layout.Context()));

        Assert.Equal("2 old Snap versions", finding.Title);
        Assert.Equal(5 * MB, finding.Bytes);
        var command = (RunTool)finding.Action!;
        Assert.Equal("pkexec sh -c 'snap remove core22 --revision=1122 && snap remove firefox --revision=4033'", command.CommandLine);

        var plan = new CleanupPlanner(new PathGuard(_layout.Paths, _layout.Sandbox.Root), new FakeTools("sh", "pkexec")).Plan([finding]);
        var step = Assert.Single(plan.Steps);
        Assert.StartsWith("Runs snap's own cleanup command as root.", step.Description);
        Assert.Empty(step.Items); // never deleted by hand: only snap removes its versions
    }

    [Fact]
    public async Task No_snap_means_no_finding()
    {
        Assert.Empty(await Run(new SnapDetector(new FakeRunner(), new FakeTools()), _layout.Context()));
    }
}
