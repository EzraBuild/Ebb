using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Cleanup;

/// <summary>Turns the findings the user picked into an exact, reviewable plan. Changes nothing.</summary>
public sealed class CleanupPlanner(PathGuard guard, IToolLocator tools)
{
    private const long MayNotFitInBin = 10L * 1024 * 1024 * 1024;

    public CleanupPlan Plan(IEnumerable<Finding> findings)
    {
        var steps = new List<PlanStep>();
        var skipped = new List<SkippedFinding>();
        foreach (var finding in findings)
        {
            var (step, reason) = Build(finding);
            if (step is not null)
                steps.Add(step);
            else
                skipped.Add(new SkippedFinding(finding, reason!));
        }
        return new CleanupPlan(steps, skipped);
    }

    private (PlanStep? Step, string? Skipped) Build(Finding finding) => finding.Action switch
    {
        RemoveItems => RemoveStep(finding, "Removes the ticked items below."),
        RunTool tool => ToolStep(finding, tool),
        RunElevated elevated => (new PlanStep
        {
            Finding = finding,
            Kind = StepKind.RunElevated,
            Description = Describe(elevated.Operation),
            CommandLine = HelperCommands.Describe(elevated.Operation),
            NeedsAdmin = true,
        }, null),
        EmptyRecycleBin bin => (new PlanStep
        {
            Finding = finding,
            Kind = StepKind.EmptyRecycleBin,
            Description = $"Empties the Recycle Bin on {bin.Drive}. Those files can't be restored after this.",
        }, null),
        CompactDisk disk => (new PlanStep
        {
            Finding = finding,
            Kind = StepKind.CompactDisk,
            Description = "Shrinks the disk file down to the space it really uses. Nothing inside it is deleted.",
            CommandLine = "wsl --shutdown, then " + HelperCommands.Describe(HelperOperation.CompactVhdx, disk.VhdxPath),
            NeedsAdmin = true,
            Warnings = disk.Owner == DiskOwner.Docker
                ? ["Quit Docker Desktop first.", "Closes all running WSL Linux apps and terminals."]
                : ["Closes all running WSL Linux apps and terminals."],
        }, null),
        OpenTarget => (null, "Ebb opens the right Windows tool for this one instead."),
        _ => (null, "There's nothing to clean."),
    };

    private (PlanStep?, string?) ToolStep(Finding finding, RunTool tool)
    {
        if (tools.Find(tool.Tool) is { } toolPath)
        {
            return (new PlanStep
            {
                Finding = finding,
                Kind = StepKind.RunTool,
                Description = $"Runs {tool.Tool}'s own cleanup command. {tool.Tool} deletes the files itself, so " +
                              "they don't go to the Recycle Bin.",
                CommandLine = tool.CommandLine,
                ToolPath = toolPath,
            }, null);
        }

        return finding.Items.Count == 0
            ? (null, $"{tool.Tool} isn't installed.")
            : RemoveStep(finding, $"{tool.Tool} isn't installed, so Ebb removes the cache folders below instead.");
    }

    private (PlanStep?, string?) RemoveStep(Finding finding, string description)
    {
        var items = finding.Items
            .Select(i => new PlanItem(i.Path, i.Bytes, i.Note, i.SelectedByDefault, guard.Check(i.Path)))
            .ToList();
        if (items.Count == 0 || items.All(i => i.Blocked is not null))
            return (null, items.FirstOrDefault()?.Blocked ?? "Nothing left to remove.");

        return (new PlanStep
        {
            Finding = finding,
            Kind = StepKind.RemoveItems,
            Description = description,
            Items = items,
            Warnings = items.Any(i => i.Bytes >= MayNotFitInBin)
                ? ["Some of these may be too big for the Recycle Bin. Windows will ask before deleting those for good."]
                : [],
        }, null);
    }

    private static string Describe(HelperOperation operation) => operation switch
    {
        HelperOperation.HibernateOff => "Turns hibernation off with Windows' own command, which removes the file. " +
                                        "Fast Startup goes off too. Undo any time with “powercfg /h on”.",
        HelperOperation.ComponentCleanup => "Runs Windows' own component cleanup. Takes a few minutes. Updates " +
                                            "installed before it can't be uninstalled afterwards.",
        _ => HelperCommands.Describe(operation),
    };
}
