using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

public sealed class DownloadsDetector : Detector
{
    private static readonly TimeSpan OldAfter = TimeSpan.FromDays(30);

    private static readonly HashSet<string> InstallerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle",
        ".appimage", ".deb", ".rpm", ".run", ".flatpak",
        ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".xz", ".bz2", ".zst", ".iso", ".img",
    };

    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var downloads = context.Paths.Downloads;
        var all = context.ItemsIn(downloads);
        var cutoff = context.NowUtc - OldAfter;
        var installers = all
            .Where(i => i.LastWriteUtc < cutoff && File.Exists(i.Path) && InstallerTypes.Contains(Path.GetExtension(i.Path)))
            .Select(i => i with { Note = $"downloaded {TimeText.Ago(i.LastWriteUtc, context.NowUtc)}" })
            .ToList();

        if (installers.Count > 0)
        {
            yield return new Finding
            {
                Id = "personal.downloads.installers",
                Category = Category.Personal,
                Title = "Old installers and archives in Downloads",
                Reason = "Setup files and zips you downloaded over a month ago. Once something is installed or " +
                         "unpacked, these are usually safe to remove.",
                Safety = Safety.CheckFirst,
                Bytes = installers.Sum(i => i.Bytes),
                Path = downloads,
                Items = installers,
                Action = new RemoveItems(),
            };
        }

        var rest = all.Sum(i => i.Bytes) - installers.Sum(i => i.Bytes);
        if (rest > 0)
        {
            yield return new Finding
            {
                Id = "personal.downloads",
                Category = Category.Personal,
                Title = "Other files in Downloads",
                Reason = "Everything else in your Downloads folder. Worth a look: old downloads pile up here.",
                Safety = Safety.CheckFirst,
                Bytes = rest,
                Path = downloads,
                Action = new OpenTarget(downloads, null, "Open Downloads"),
            };
        }
    }
}
