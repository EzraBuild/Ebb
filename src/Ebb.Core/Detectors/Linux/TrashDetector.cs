using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>Linux: the Trash in your home folder.</summary>
public sealed class TrashDetector(IRecycleBin trash) : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var folder = context.Paths.Trash;
        var info = trash.Query(folder);
        if (info.Bytes <= 0)
            yield break;

        yield return new Finding
        {
            Id = "linux.trash",
            Category = Category.Windows, // shown as "System" on Linux
            Title = "Trash",
            Reason = $"{info.Items:N0} things you deleted. They still take space until the Trash is emptied, and " +
                     "can't be restored after that.",
            Safety = Safety.CheckFirst,
            Bytes = info.Bytes,
            Path = folder,
            Action = new EmptyRecycleBin(folder, IsTrash: true),
        };
    }
}
