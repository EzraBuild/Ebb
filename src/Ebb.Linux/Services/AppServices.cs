using Ebb.Core.Cleanup;
using Ebb.Core.Detectors;
using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Core.Scanning;
using Ebb.Core.Vhdx;

namespace Ebb.Linux.Services;

/// <summary>Everything the UI needs from Ebb.Core, wired up once for Linux.</summary>
public sealed class AppServices
{
    public required SystemPaths Paths { get; init; }

    /// <summary>A scanner for one disk: it stays on that disk and skips /proc and the like.</summary>
    public required Func<string, DriveScanner> Scanner { get; init; }

    public required FindingEngine Engine { get; init; }
    public required CleanupPlanner Planner { get; init; }
    public required CleanupExecutor Executor { get; init; }
    public required CleanupLog Log { get; init; }

    public static AppServices CreateDefault(IReadOnlyList<Mount> mounts)
    {
        var paths = SystemPaths.FromEnvironment();
        var runner = new ProcessCommandRunner();
        var tools = new PathToolLocator();
        var trash = new FreedesktopTrash(paths.Trash);
        var guard = new PathGuard(paths);
        var log = new CleanupLog(CleanupLog.DefaultPath);

        return new AppServices
        {
            Paths = paths,
            Scanner = root => new DriveScanner(new ScanOptions { SkipFolder = LinuxMounts.SkipFor(root, mounts) }),
            Engine = new FindingEngine(DefaultDetectors.CreateLinux(runner, tools, trash)),
            Planner = new CleanupPlanner(guard, tools),
            Executor = new CleanupExecutor(guard, new LinuxFileRemover(trash), runner, trash, new NoHelper(), new NoHelper(), log),
            Log = log,
        };
    }

    /// <summary>The Windows admin helper and disk shrinking have no Linux findings that use them.</summary>
    private sealed class NoHelper : IElevatedRunner, IDiskCompactor
    {
        public Task<HelperResult> RunAsync(HelperRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HelperResult(false, "Not available on Linux."));

        public Task<CompactResult> CompactAsync(CompactDisk disk, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CompactResult(false, "Not available on Linux.", 0, 0));
    }
}
