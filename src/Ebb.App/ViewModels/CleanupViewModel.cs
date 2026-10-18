using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ebb.App.Services;
using Ebb.Core;
using Ebb.Core.Cleanup;

namespace Ebb.App.ViewModels;

public enum CleanupState
{
    Review,
    Running,
    Done,
}

/// <summary>The review window: shows the exact plan, runs it only after the user confirms.</summary>
public sealed partial class CleanupViewModel : ObservableObject
{
    private readonly CleanupExecutor _executor;
    private readonly string _logPath;
    private readonly IDialogs _dialogs;

    public CleanupViewModel(CleanupPlan plan, CleanupExecutor executor, string logPath, IDialogs dialogs)
    {
        Plan = plan;
        _executor = executor;
        _logPath = logPath;
        _dialogs = dialogs;
        Steps = plan.Steps.Select(s => new StepViewModel(s, plan, TotalsChanged)).ToList();
        Skipped = plan.Skipped.Select(s => new SkippedViewModel(s.Finding.Title, s.Reason)).ToList();
    }

    public CleanupPlan Plan { get; }

    public IReadOnlyList<StepViewModel> Steps { get; }

    public IReadOnlyList<SkippedViewModel> Skipped { get; }

    public bool HasSkipped => Skipped.Count > 0;

    public ObservableCollection<ResultViewModel> Results { get; } = [];

    public CleanupReport? Report { get; private set; }

    public event Action? CloseRequested;

    [ObservableProperty]
    private bool _useRecycleBin = true;

    partial void OnUseRecycleBinChanged(bool value)
    {
        Plan.RemovalMode = value ? RemovalMode.RecycleBin : RemovalMode.Permanent;
        foreach (var step in Steps)
            step.RemovalModeChanged();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReview), nameof(IsRunning), nameof(IsDone), nameof(Eyebrow), nameof(Heading), nameof(Subheading), nameof(FooterText))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    private CleanupState _state;

    public bool IsReview => State == CleanupState.Review;

    public bool IsRunning => State == CleanupState.Running;

    public bool IsDone => State == CleanupState.Done;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private string _progressText = "";

    public bool HasRemovals => Steps.Any(s => s.HasItems);

    public string Eyebrow => State switch
    {
        CleanupState.Review => "REVIEW",
        CleanupState.Running => "CLEANING UP",
        _ => "DONE",
    };

    public string Heading => State switch
    {
        CleanupState.Review => "Here's exactly what will happen",
        CleanupState.Running => "Cleaning up…",
        _ => Report is { } r ? Summary(r) : "Done",
    };

    public string Subheading => State switch
    {
        CleanupState.Review => "Nothing changes until you press Clean up. Untick anything you want to keep.",
        CleanupState.Running => "Windows may ask for admin for some steps.",
        _ => "Everything Ebb did is written to the cleanup log.",
    };

    public string FooterText => State switch
    {
        CleanupState.Review => $"{Display.Count(Steps.Count, "step", "steps")} · frees up to {ByteSize.Format(Plan.EstimatedBytes)}" +
                               (Plan.NeedsAdmin ? " · asks for admin" : ""),
        CleanupState.Running => ProgressText,
        _ => "",
    };

    public string ConfirmText => Plan.EstimatedBytes > 0 ? $"Clean up {ByteSize.Format(Plan.EstimatedBytes)}" : "Clean up";

    private bool CanClean => IsReview && Steps.Any(s => !s.HasItems || s.Step.SelectedItems.Any());

    [RelayCommand(CanExecute = nameof(CanClean), IncludeCancelCommand = true)]
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        State = CleanupState.Running;
        var progress = new Progress<CleanupProgress>(p => ProgressText = $"Step {p.Step} of {p.StepCount} · {p.Message}");
        Report = await _executor.ExecuteAsync(Plan, progress, cancellationToken);
        foreach (var result in Report.Results)
            Results.Add(new ResultViewModel(result));
        State = CleanupState.Done;
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void OpenLog() => _dialogs.Reveal(_logPath);

    private void TotalsChanged()
    {
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(ConfirmText));
        CleanCommand.NotifyCanExecuteChanged();
    }

    private static string Summary(CleanupReport report)
    {
        var text = $"Freed {ByteSize.Format(report.FreedBytes)}";
        if (report.RecycledBytes > 0)
            text += $", {ByteSize.Format(report.RecycledBytes)} in the Recycle Bin";
        return text;
    }
}

public sealed partial class StepViewModel : ObservableObject
{
    private readonly CleanupPlan _plan;
    private readonly Action _totalsChanged;

    public StepViewModel(PlanStep step, CleanupPlan plan, Action totalsChanged)
    {
        Step = step;
        _plan = plan;
        _totalsChanged = totalsChanged;
        Items = step.Items.Select(i => new PlanItemViewModel(i, Changed)).ToList();
    }

    public PlanStep Step { get; }

    public string Title => Step.Title;

    public string Description => Step.Description;

    public string? CommandText => Step.CommandLine is { } c ? "> " + c : null;

    public bool HasCommand => Step.CommandLine is not null;

    public IReadOnlyList<string> Warnings => Step.Warnings;

    public bool NeedsAdmin => Step.NeedsAdmin;

    public bool IsPermanent => Step.IsPermanent(_plan.RemovalMode);

    public IReadOnlyList<PlanItemViewModel> Items { get; }

    public bool HasItems => Items.Count > 0;

    public string SizeText => Step.EstimatedBytes > 0 ? ByteSize.Format(Step.EstimatedBytes) : "—";

    public string ItemsSummary
    {
        get
        {
            var ticked = Items.Count(i => i.Selected);
            var verb = Step.RemovalFor(_plan.RemovalMode) == RemovalMode.RecycleBin ? "go to the Recycle Bin" : "get deleted for good";
            return $"{ticked:N0} of {Items.Count:N0} ticked · these {verb}";
        }
    }

    [RelayCommand]
    private void TickAll() => SetAll(true);

    [RelayCommand]
    private void UntickAll() => SetAll(false);

    public void RemovalModeChanged()
    {
        OnPropertyChanged(nameof(IsPermanent));
        OnPropertyChanged(nameof(ItemsSummary));
    }

    private void SetAll(bool selected)
    {
        foreach (var item in Items.Where(i => i.CanSelect))
            item.Selected = selected;
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(ItemsSummary));
        _totalsChanged();
    }
}

public sealed partial class PlanItemViewModel(PlanItem item, Action changed) : ObservableObject
{
    public string Path => item.Path;

    public string SizeText => ByteSize.Format(item.Bytes);

    public string? Note => item.Blocked ?? item.Note;

    public bool CanSelect => item.Blocked is null;

    public bool Selected
    {
        get => item.Selected;
        set
        {
            if (item.Selected == value || !CanSelect)
                return;
            item.Selected = value;
            OnPropertyChanged();
            changed();
        }
    }
}

public sealed record SkippedViewModel(string Title, string Reason);

public sealed class ResultViewModel(StepResult result)
{
    public string Title => result.Step.Title;

    public string Message => result.Message;

    public string Symbol => result.Outcome switch
    {
        StepOutcome.Done => "✓",
        StepOutcome.Partly => "!",
        StepOutcome.Failed => "×",
        _ => "–",
    };

    public string SizeText => result.FreedBytes + result.RecycledBytes > 0
        ? ByteSize.Format(result.FreedBytes + result.RecycledBytes)
        : "";
}
