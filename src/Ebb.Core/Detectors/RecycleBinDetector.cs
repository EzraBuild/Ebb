using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

public sealed class RecycleBinDetector(IRecycleBin recycleBin) : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var drive = Path.GetPathRoot(context.Root)!;
        var info = recycleBin.Query(drive);
        if (info.Bytes <= 0)
            yield break;

        yield return new Finding
        {
            Id = "win.recycle-bin",
            Category = Category.Windows,
            Title = "Recycle Bin",
            Reason = $"{info.Items:N0} things you deleted. They still take space until the bin is emptied, and " +
                     "can't be restored after that.",
            Safety = Safety.CheckFirst,
            Bytes = info.Bytes,
            Path = Path.Combine(drive, "$Recycle.Bin"),
            Action = new EmptyRecycleBin(drive),
        };
    }
}
