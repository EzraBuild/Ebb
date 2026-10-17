using Ebb.Core.Findings;

namespace Ebb.Core.Cleanup;

public enum StepKind
{
    RemoveItems,
    RunTool,
    RunElevated,
    EmptyRecycleBin,
    CompactDisk,
}

/// <summary>One file or folder the user can see and untick before anything happens.</summary>
public sealed class PlanItem(string path, long bytes, string? note, bool selected, string? blocked)
{
    private bool _selected = selected;

    public string Path { get; } = path;

    public long Bytes { get; } = bytes;

    public string? Note { get; } = note;

    /// <summary>Why Ebb won't remove it. Blocked items can't be selected.</summary>
    public string? Blocked { get; } = blocked;

    public bool Selected
    {
        get => _selected && Blocked is null;
        set => _selected = value;
    }
}

/// <summary>What will happen for one finding, spelled out exactly.</summary>
public sealed class PlanStep
{
    public required Finding Finding { get; init; }

    public required StepKind Kind { get; init; }

    /// <summary>Plain words: what happens and what it means.</summary>
    public required string Description { get; init; }

    /// <summary>The exact command, for steps that run one.</summary>
    public string? CommandLine { get; init; }

    public IReadOnlyList<PlanItem> Items { get; init; } = [];

    public bool NeedsAdmin { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string? ToolPath { get; init; }

    public string Title => Finding.Title;

    public IEnumerable<PlanItem> SelectedItems => Items.Where(i => i.Selected);

    public long EstimatedBytes => Kind == StepKind.RemoveItems ? SelectedItems.Sum(i => i.Bytes) : Finding.Bytes ?? 0;

    /// <summary>True when the result can't be undone from the Recycle Bin.</summary>
    public bool IsPermanent(RemovalMode mode) => Kind switch
    {
        StepKind.RemoveItems => mode == RemovalMode.Permanent,
        StepKind.CompactDisk => false, // deletes nothing
        _ => true,
    };
}

public sealed record SkippedFinding(Finding Finding, string Reason);

public sealed class CleanupPlan(IReadOnlyList<PlanStep> steps, IReadOnlyList<SkippedFinding> skipped)
{
    public IReadOnlyList<PlanStep> Steps { get; } = steps;

    public IReadOnlyList<SkippedFinding> Skipped { get; } = skipped;

    public RemovalMode RemovalMode { get; set; } = RemovalMode.RecycleBin;

    public long EstimatedBytes => Steps.Sum(s => s.EstimatedBytes);

    public bool NeedsAdmin => Steps.Any(s => s.NeedsAdmin);
}
