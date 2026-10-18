using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

/// <summary>node_modules folders in your own JavaScript projects.</summary>
public sealed class NodeModulesDetector : Detector
{
    private static readonly TimeSpan Recent = TimeSpan.FromDays(14);

    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        // Apps and global tools ship node_modules too (VS Code extensions, global npm). Never touch those.
        string[] notProjects = [.. p.SystemFolders, .. p.AppDataFolders];

        var items = new List<FindingItem>();
        foreach (var (path, node) in context.Scan.FindDirectories("node_modules"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = Path.GetDirectoryName(path)!;
            if (notProjects.Any(folder => PathUtil.IsUnder(path, folder)) || InToolOrWindowsFolder(path) ||
                !File.Exists(Path.Combine(project, "package.json")))
                continue;

            var lastUsed = Max(Directory.GetLastWriteTimeUtc(project), Directory.GetLastWriteTimeUtc(path));
            var recent = context.NowUtc - lastUsed < Recent;
            items.Add(new FindingItem(path, node.Size, lastUsed,
                $"{Path.GetFileName(project)}, changed {TimeText.Ago(lastUsed, context.NowUtc)}",
                SelectedByDefault: !recent));
        }

        if (items.Count == 0)
            yield break;

        yield return new Finding
        {
            Id = "dev.node-modules",
            Category = Category.Developer,
            Title = items.Count == 1 ? "node_modules folder" : $"node_modules in {items.Count} projects",
            Reason = "Packages your JavaScript projects downloaded. Running npm install (or pnpm or yarn) in a " +
                     "project brings them back. Projects you changed in the last 2 weeks are left unticked.",
            Safety = Safety.Safe,
            Bytes = items.Sum(i => i.Bytes),
            Items = items.OrderByDescending(i => i.Bytes).ToList(),
            Action = new RemoveItems(RebuiltBy: "Running npm install (or pnpm, yarn) in a project"),
        };
    }

    // Folders like .vscode, .cursor or .nvm belong to tools, not to your projects. Ones like $Recycle.Bin
    // (projects you already deleted) and System Volume Information belong to Windows.
    private static bool InToolOrWindowsFolder(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(segment =>
            segment.StartsWith('.') || segment.StartsWith('$') ||
            segment.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase));

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
