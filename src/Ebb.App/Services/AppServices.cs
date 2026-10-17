using Ebb.Core.Cleanup;
using Ebb.Core.Detectors;
using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Core.Scanning;
using Ebb.Core.Vhdx;

namespace Ebb.App.Services;

/// <summary>Everything the UI needs from Ebb.Core, wired up once.</summary>
public sealed class AppServices
{
    public required SystemPaths Paths { get; init; }
    public required DriveScanner Scanner { get; init; }
    public required FindingEngine Engine { get; init; }
    public required CleanupPlanner Planner { get; init; }
    public required CleanupExecutor Executor { get; init; }
    public required CleanupLog Log { get; init; }

    public static AppServices CreateDefault()
    {
        var paths = SystemPaths.FromEnvironment();
        var runner = new ProcessCommandRunner();
        var tools = new PathToolLocator();
        var recycleBin = new ShellRecycleBin();
        var elevated = new HelperProcessRunner(HelperProcessRunner.DefaultPath);
        var guard = new PathGuard(paths);
        var log = new CleanupLog(CleanupLog.DefaultPath);

        return new AppServices
        {
            Paths = paths,
            Scanner = new DriveScanner(),
            Engine = new FindingEngine(DefaultDetectors.Create(new RegistryWslDistroSource(), runner, tools, recycleBin)),
            Planner = new CleanupPlanner(guard, tools),
            Executor = new CleanupExecutor(guard, new ShellFileRemover(), runner, recycleBin, elevated,
                new VhdxCompactor(runner, elevated, new ProcessInspector()), log),
            Log = log,
        };
    }
}
