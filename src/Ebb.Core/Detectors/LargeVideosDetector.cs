using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

/// <summary>Big videos in your own folders. Listed only; nothing is ticked for you.</summary>
public sealed class LargeVideosDetector(long minBytes = 100L * 1024 * 1024) : Detector
{
    private static readonly HashSet<string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".flv", ".3gp",
    };

    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var videos = context.Scan.LargeFiles
            .Where(f => f.Size >= minBytes && VideoTypes.Contains(Path.GetExtension(f.Path)))
            .Where(f => PathUtil.IsUnder(f.Path, p.UserProfile) && !p.AppDataFolders.Any(folder => PathUtil.IsUnder(f.Path, folder)))
            .Select(f => new FindingItem(f.Path, f.Size, f.LastWriteUtc,
                $"changed {TimeText.Ago(f.LastWriteUtc, context.NowUtc)}", SelectedByDefault: false))
            .ToList();
        if (videos.Count == 0)
            yield break;

        yield return new Finding
        {
            Id = "personal.videos",
            Category = Category.Personal,
            Title = videos.Count == 1 ? "Large video" : $"{videos.Count} large videos",
            Reason = $"Your own videos, {ByteSize.Format(minBytes)} or bigger. Ebb won't pick any for you: tick the " +
                     "ones you don't need, or move them to another drive or the cloud.",
            Safety = Safety.CheckFirst,
            Bytes = videos.Sum(v => v.Bytes),
            Items = videos,
            Action = new RemoveItems(),
        };
    }
}
