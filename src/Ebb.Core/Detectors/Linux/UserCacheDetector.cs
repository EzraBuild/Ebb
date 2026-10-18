using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

/// <summary>Linux: what apps keep in ~/.cache, plus what AUR helpers keep from building packages.</summary>
public sealed class UserCacheDetector : Detector
{
    private static readonly TimeSpan InUse = TimeSpan.FromDays(1);

    // Other findings cover these (with the tool's own command where there is one).
    private static readonly HashSet<string> CoveredElsewhere = new(StringComparer.Ordinal)
    {
        "pip", "yarn", "go-build", "yay", "paru",
    };

    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var cache = context.Paths.Cache;
        var now = context.NowUtc;

        var aur = new[] { "yay", "paru" }
            .SelectMany(helper => context.ItemsIn(Path.Combine(cache, helper)))
            .Select(i => i with { Note = $"{Path.GetFileName(Path.GetDirectoryName(i.Path))}, built {TimeText.Ago(i.LastWriteUtc, now)}" })
            .ToList();
        if (aur.Count > 0)
        {
            yield return new Finding
            {
                Id = "linux.cache.aur",
                Category = Category.Windows, // shown as "System" on Linux
                Title = "AUR build files",
                Reason = "What yay or paru downloaded and built to install AUR packages. Your installed packages " +
                         "don't need them.",
                Safety = Safety.Safe,
                Bytes = aur.Sum(i => i.Bytes),
                Items = aur,
                Action = new RemoveItems(RebuiltBy: "Updating an AUR package"),
            };
        }

        var apps = context.ItemsIn(cache, info => !CoveredElsewhere.Contains(info.Name))
            .Where(i => i.Bytes >= 1024 * 1024)
            .Select(i =>
            {
                var recent = now - i.LastWriteUtc < InUse;
                return i with
                {
                    Note = recent ? "used today, so the app may be open" : $"changed {TimeText.Ago(i.LastWriteUtc, now)}",
                    SelectedByDefault = !recent,
                };
            })
            .ToList();
        if (apps.Count > 0)
        {
            yield return new Finding
            {
                Id = "linux.cache.apps",
                Category = Category.Windows, // shown as "System" on Linux
                Title = "App caches",
                Reason = "Files apps keep in ~/.cache to start and load faster: browser caches, thumbnails, shader " +
                         "caches. Apps make them again when needed. Ones used today are left unticked.",
                Safety = Safety.Safe,
                Bytes = apps.Sum(i => i.Bytes),
                Path = cache,
                Items = apps,
                Action = new RemoveItems(RebuiltBy: "The app that made each one"),
            };
        }
    }
}
