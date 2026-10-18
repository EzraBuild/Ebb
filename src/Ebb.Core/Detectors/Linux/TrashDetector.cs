using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>Linux: the Trash in your home folder, and your Trash on the scanned drive when it's another one.</summary>
/// <param name="uid">Your user id, which names Trash folders on other drives. By default, the current user's.</param>
public sealed class TrashDetector(IRecycleBin trash, string? uid = null) : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var home = context.Paths.Trash;
        var folders = new[] { home }
            .Concat(FreedesktopTrash.OnDrive(context.Root, uid ?? LinuxUser.Id))
            .Distinct(PathUtil.Comparer);

        foreach (var folder in folders)
        {
            var info = trash.Query(folder);
            if (info.Bytes <= 0)
                continue;

            var onThisDrive = !PathUtil.SamePath(folder, home);
            yield return new Finding
            {
                Id = onThisDrive ? "linux.trash:" + folder : "linux.trash",
                Category = Category.Windows, // shown as "System" on Linux
                Title = onThisDrive ? "Trash on this drive" : "Trash",
                Reason = $"{info.Items:N0} things you deleted{(onThisDrive ? " from this drive" : "")}. They still take " +
                         "space until the Trash is emptied, and can't be restored after that.",
                Safety = Safety.CheckFirst,
                Bytes = info.Bytes,
                Path = folder,
                Action = new EmptyRecycleBin(folder, IsTrash: true),
            };
        }
    }
}
