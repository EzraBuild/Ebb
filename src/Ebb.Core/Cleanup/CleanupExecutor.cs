using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Core.Scanning;
using Ebb.Core.Vhdx;

namespace Ebb.Core.Cleanup;

public sealed record CleanupProgress(int Step, int StepCount, string Message);

public enum StepOutcome
{
    Done,
    Partly,
    Failed,
    Skipped,
}

public sealed record StepResult(PlanStep Step, StepOutcome Outcome, long FreedBytes, long RecycledBytes, string Message);

public sealed class CleanupReport(IReadOnlyList<StepResult> results)
{
    public IReadOnlyList<StepResult> Results { get; } = results;

    /// <summary>Space that's free right now.</summary>
    public long FreedBytes => Results.Sum(r => r.FreedBytes);

    /// <summary>Space that moved to the Recycle Bin. It's freed when the bin is emptied.</summary>
    public long RecycledBytes => Results.Sum(r => r.RecycledBytes);
}

/// <summary>
/// Carries out an approved plan exactly: only ticked items, each one checked by the guard again
/// right before it goes, and every action written to the log.
/// </summary>
public sealed class CleanupExecutor(
    PathGuard guard,
    IFileRemover remover,
    ICommandRunner runner,
    IRecycleBin recycleBin,
    IElevatedRunner elevated,
    IDiskCompactor compactor,
    CleanupLog log)
{
    public async Task<CleanupReport> ExecuteAsync(CleanupPlan plan, IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<StepResult>();
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(new StepResult(step, StepOutcome.Skipped, 0, 0, "Stopped before this step."));
                continue;
            }

            progress?.Report(new CleanupProgress(i + 1, plan.Steps.Count, step.Title));
            try
            {
                results.Add(step.Kind switch
                {
                    StepKind.RemoveItems => await RemoveAsync(step, step.RemovalFor(plan.RemovalMode), cancellationToken),
                    StepKind.RunTool => await RunToolAsync(step, cancellationToken),
                    StepKind.RunElevated => await RunElevatedAsync(step, cancellationToken),
                    StepKind.EmptyRecycleBin => EmptyBin(step),
                    StepKind.CompactDisk => await CompactAsync(step, cancellationToken),
                    _ => new StepResult(step, StepOutcome.Skipped, 0, 0, "Unknown step."),
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log(step, step.Kind.ToString(), step.Finding.Path ?? step.Title, 0, false, ex.Message);
                results.Add(new StepResult(step, StepOutcome.Failed, 0, 0, ex.Message));
            }
            catch (OperationCanceledException)
            {
                results.Add(new StepResult(step, StepOutcome.Skipped, 0, 0, "Stopped."));
            }
        }
        return new CleanupReport(results);
    }

    private async Task<StepResult> RemoveAsync(PlanStep step, RemovalMode mode, CancellationToken cancellationToken)
    {
        var selected = step.SelectedItems.ToList();
        if (selected.Count == 0)
            return new StepResult(step, StepOutcome.Skipped, 0, 0, "Nothing was ticked.");

        var action = mode == RemovalMode.RecycleBin ? "recycle" : "delete";
        long bytes = 0;
        var removed = 0;
        var problems = new List<string>();
        foreach (var item in selected)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            if (!File.Exists(item.Path) && !Directory.Exists(item.Path))
                continue; // already gone, nothing to do

            if (guard.Check(item.Path) is { } blocked)
            {
                Log(step, "refuse", item.Path, 0, false, blocked);
                problems.Add($"{Path.GetFileName(item.Path)}: {blocked}");
                continue;
            }

            try
            {
                await Task.Run(() => remover.Remove(item.Path, mode), CancellationToken.None);
                Log(step, action, item.Path, item.Bytes, true, null);
                bytes += item.Bytes;
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or RemovalDeclinedException)
            {
                Log(step, action, item.Path, 0, false, ex.Message);
                problems.Add($"{Path.GetFileName(item.Path)}: {ex.Message}");
            }
        }

        var size = ByteSize.Format(bytes);
        var message = mode == RemovalMode.RecycleBin
            ? $"Moved {Count(removed)} ({size}) to the Recycle Bin."
            : $"Deleted {Count(removed)} ({size}).";
        if (problems.Count > 0)
            message += $" {Count(problems.Count)} couldn't be removed, e.g. {problems[0]}";

        var outcome = problems.Count == 0 ? StepOutcome.Done : removed == 0 ? StepOutcome.Failed : StepOutcome.Partly;
        return mode == RemovalMode.RecycleBin
            ? new StepResult(step, outcome, 0, bytes, message)
            : new StepResult(step, outcome, bytes, 0, message);
    }

    private async Task<StepResult> RunToolAsync(PlanStep step, CancellationToken cancellationToken)
    {
        var tool = (RunTool)step.Finding.Action!;
        var folders = step.Finding.Items
            .Select(i => Path.GetDirectoryName(i.Path)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var before = folders.Sum(f => DirectorySizer.Measure(f, cancellationToken));

        var result = await runner.RunAsync(step.ToolPath!, tool.Arguments, TimeSpan.FromMinutes(30), cancellationToken);

        var freed = Math.Max(0, before - folders.Sum(f => DirectorySizer.Measure(f, cancellationToken)));
        var output = FirstLines(result.Succeeded ? result.Output : result.Error + result.Output);
        Log(step, "command", step.CommandLine!, freed, result.Succeeded, output);

        if (!result.Succeeded)
            return new StepResult(step, StepOutcome.Failed, 0, 0, $"{tool.Tool} reported a problem: {output}");

        var message = tool.Tool == "docker"
            ? "Docker cleaned up inside its disk. Shrink the Docker disk to get that space back on this drive."
            : $"{tool.Tool} finished and freed {ByteSize.Format(freed)}.";
        return new StepResult(step, StepOutcome.Done, freed, 0, message);
    }

    private async Task<StepResult> RunElevatedAsync(PlanStep step, CancellationToken cancellationToken)
    {
        var operation = ((RunElevated)step.Finding.Action!).Operation;
        var result = await elevated.RunAsync(new HelperRequest(operation), cancellationToken);
        var freed = Math.Max(0, result.FreedBytes ?? 0);
        Log(step, "admin-command", step.CommandLine!, freed, result.Succeeded, result.Message);
        return new StepResult(step, result.Succeeded ? StepOutcome.Done : StepOutcome.Failed, freed, 0, result.Message);
    }

    private StepResult EmptyBin(PlanStep step)
    {
        var drive = ((EmptyRecycleBin)step.Finding.Action!).Drive;
        recycleBin.Empty(drive);
        var freed = step.Finding.Bytes ?? 0;
        Log(step, "empty-recycle-bin", drive, freed, true, null);
        return new StepResult(step, StepOutcome.Done, freed, 0, $"Emptied the Recycle Bin and freed {ByteSize.Format(freed)}.");
    }

    private async Task<StepResult> CompactAsync(PlanStep step, CancellationToken cancellationToken)
    {
        var disk = (CompactDisk)step.Finding.Action!;
        var result = await compactor.CompactAsync(disk, cancellationToken);
        Log(step, "compact", disk.VhdxPath, result.Freed, result.Succeeded, result.Message);
        return new StepResult(step, result.Succeeded ? StepOutcome.Done : StepOutcome.Failed, result.Freed, 0, result.Message);
    }

    private void Log(PlanStep step, string action, string target, long bytes, bool succeeded, string? message) =>
        log.Write(new LogEntry(DateTimeOffset.Now, step.Finding.Id, action, target, bytes, succeeded, message));

    private static string Count(int n) => n == 1 ? "1 item" : $"{n:N0} items";

    private static string FirstLines(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", lines.Take(3));
    }
}
