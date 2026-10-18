using System.Text.RegularExpressions;
using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>
/// Linux (mostly Ubuntu): the older version Snap keeps of each app after an update. Removed with snap's own
/// command, as root.
/// </summary>
public sealed partial class SnapDetector(ICommandRunner runner, IToolLocator tools) : IDetector
{
    public sealed record OldRevision(string Name, string Revision);

    public async Task<IReadOnlyList<Finding>> DetectAsync(ScanContext context, CancellationToken cancellationToken)
    {
        if (tools.Find("snap") is not { } snap)
            return [];
        var list = await runner.RunAsync(snap, ["list", "--all"], TimeSpan.FromSeconds(20), cancellationToken);
        var old = list.Succeeded ? Disabled(list.Output) : [];
        if (old.Count == 0)
            return [];

        // Each version is one .snap file; their sizes are what removing them frees.
        var folder = Path.Combine(context.Paths.SystemDrive, "var", "lib", "snapd", "snaps");
        var items = old
            .Select(o => new FindingItem(Path.Combine(folder, $"{o.Name}_{o.Revision}.snap"), 0, DateTime.MinValue, $"{o.Name}, version {o.Revision}"))
            .Select(i => i with { Bytes = context.SizeOf(i.Path) })
            .ToList();
        var script = string.Join(" && ", old.Select(o => $"snap remove {o.Name} --revision={o.Revision}"));

        return
        [
            new Finding
            {
                Id = "linux.snap.old",
                Category = Category.Windows, // shown as "System" on Linux
                Title = old.Count == 1 ? "Old Snap version" : $"{old.Count} old Snap versions",
                Reason = "After an update, Snap keeps the previous version of each app in case you want to go back. " +
                         "These aren't in use. Snap removes them itself.",
                Safety = Safety.Safe,
                Bytes = items.Sum(i => i.Bytes),
                Path = folder,
                Items = items,
                Action = new RunTool("sh", ["-c", script], AsRoot: true, Name: "snap"),
            },
        ];
    }

    /// <summary>
    /// The disabled (old) revisions in <c>snap list --all</c>. Only plain names and revision numbers are kept,
    /// since they go into a command.
    /// </summary>
    public static IReadOnlyList<OldRevision> Disabled(string snapListOutput) =>
        snapListOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(columns => columns.Length >= 6 && columns[^1].Split(',').Contains("disabled"))
            .Select(columns => new OldRevision(columns[0], columns[2]))
            .Where(o => NamePattern().IsMatch(o.Name) && RevisionPattern().IsMatch(o.Revision))
            .ToList();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^x?\d+$")]
    private static partial Regex RevisionPattern();
}
