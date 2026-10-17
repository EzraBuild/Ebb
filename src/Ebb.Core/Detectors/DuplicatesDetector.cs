using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

public sealed class DuplicatesDetector(DuplicateFinder finder) : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var roots = context.Paths.PersonalFolders.Where(context.Covers);
        var groups = finder.Find(roots, cancellationToken);
        var copies = groups
            .SelectMany(g => g.Files.Skip(1).Select(copy => new FindingItem(copy.Path, copy.Size, copy.LastWriteUtc,
                "same as " + Shorten(g.Files[0].Path, context.Paths.UserProfile))))
            .ToList();
        if (copies.Count == 0)
            yield break;

        yield return new Finding
        {
            Id = "personal.duplicates",
            Category = Category.Personal,
            Title = copies.Count == 1 ? "Duplicate file" : $"{copies.Count} duplicate files",
            Reason = "Exact copies: the same content, byte for byte. The oldest copy of each file is always kept, " +
                     "and only the extra copies are listed.",
            Safety = Safety.CheckFirst,
            Bytes = copies.Sum(c => c.Bytes),
            Items = copies,
            Action = new RemoveItems(),
        };
    }

    private static string Shorten(string path, string profile) =>
        PathUtil.IsUnder(path, profile) ? Path.Join("~", Path.GetRelativePath(profile, path)) : path;
}
