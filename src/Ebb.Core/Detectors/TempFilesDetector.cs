using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

public sealed class TempFilesDetector : Detector
{
    private static readonly TimeSpan MinAge = TimeSpan.FromDays(1);

    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var cutoff = context.NowUtc - MinAge;
        var items = context.ItemsIn(p.Temp, info => info.LastWriteTimeUtc < cutoff);
        if (items.Count > 0)
        {
            yield return new Finding
            {
                Id = "win.temp",
                Category = Category.Windows,
                Title = "Temporary files",
                Reason = "Files apps left behind in your temp folder. Only items older than a day are included, " +
                         "and anything still in use is skipped.",
                Safety = Safety.Safe,
                Bytes = items.Sum(i => i.Bytes),
                Path = p.Temp,
                Items = items,
                Action = new RemoveItems(),
            };
        }

        var windowsTemp = Path.Combine(p.Windows, "Temp");
        if (context.SizeOf(windowsTemp) is var bytes and > 0)
        {
            yield return new Finding
            {
                Id = "win.windows-temp",
                Category = Category.Windows,
                Title = "Windows temp folder",
                Reason = "Temporary files Windows itself made. Ebb doesn't touch system folders: Disk Cleanup " +
                         "removes these safely (tick “Temporary files”).",
                Safety = Safety.Safe,
                Bytes = bytes,
                Path = windowsTemp,
                Action = WindowsTools.DiskCleanup(p),
            };
        }
    }
}

internal static class WindowsTools
{
    public static OpenTarget DiskCleanup(Platform.SystemPaths paths) =>
        new("cleanmgr.exe", "/d " + paths.SystemDrive[..2], "Open Disk Cleanup");
}
