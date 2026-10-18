using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

public static class DefaultDetectors
{
    public static IReadOnlyList<IDetector> Create(IWslDistroSource wsl, ICommandRunner runner, IToolLocator tools,
        IRecycleBin recycleBin) =>
    [
        new WslDiskDetector(wsl),
        new DockerDetector(wsl, runner, tools),
        new NodeModulesDetector(),
        new PackageCacheDetector(),
        new DotnetSdkDetector(),
        new VisualStudioDetector(),
        new TempFilesDetector(),
        new WindowsUpdateDetector(),
        new SystemFilesDetector(),
        new RecycleBinDetector(recycleBin),
        new DownloadsDetector(),
        new LargeVideosDetector(),
        new DuplicatesDetector(new DuplicateFinder()),
    ];

    public static IReadOnlyList<IDetector> CreateLinux(ICommandRunner runner, IToolLocator tools, IRecycleBin trash) =>
    [
        new NodeModulesDetector(),
        new PackageCacheDetector(),
        new DockerDetector(new NoWslDistros(), runner, tools),
        new SystemPackageCacheDetector(tools),
        new JournalDetector(runner, tools),
        new FlatpakDetector(tools),
        new SnapDetector(runner, tools),
        new UserCacheDetector(),
        new TrashDetector(trash),
        new DownloadsDetector(),
        new LargeVideosDetector(),
        new DuplicatesDetector(new DuplicateFinder()),
    ];
}
