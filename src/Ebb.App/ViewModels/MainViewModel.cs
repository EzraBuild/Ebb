using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ebb.App.Services;
using Ebb.Core;
using Ebb.Core.Cleanup;
using Ebb.Core.Findings;
using Ebb.Core.Scanning;

namespace Ebb.App.ViewModels;

public enum ScanState
{
    NotScanned,
    Scanning,
    Ready,
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogs _dialogs;

    public MainViewModel(AppServices services, IDialogs dialogs, IReadOnlyList<DriveViewModel> drives)
    {
        _services = services;
        _dialogs = dialogs;
        Drives = drives;
        _selectedDrive = drives.FirstOrDefault(d => PathUtil.SamePath(d.Root, services.Paths.SystemDrive)) ?? drives.FirstOrDefault();

        Developer = new FindingsPageViewModel(Category.Developer, "Developer files",
            "Caches, packages and virtual disks from your dev tools. Most of it comes back by itself when you need it.");
        Windows = new FindingsPageViewModel(Category.Windows, "Windows",
            "Temporary files, update leftovers and system files. Anything Windows owns is handled with Windows' own tools.");
        Personal = new FindingsPageViewModel(Category.Personal, "Personal files",
            "Your own files. Ebb shows you what's big or duplicated, but never picks your files for you.");
        Folders = new FoldersPageViewModel(path => _dialogs.Reveal(path));
        Pages = [Developer, Windows, Personal, Folders];
        _selectedPage = Developer;
    }

    public IReadOnlyList<DriveViewModel> Drives { get; }

    public FindingsPageViewModel Developer { get; }

    public FindingsPageViewModel Windows { get; }

    public FindingsPageViewModel Personal { get; }

    public FoldersPageViewModel Folders { get; }

    public IReadOnlyList<PageViewModel> Pages { get; }

    private IEnumerable<FindingsPageViewModel> FindingPages => [Developer, Windows, Personal];

    private IEnumerable<FindingViewModel> AllFindings => FindingPages.SelectMany(p => p.Findings);

    [ObservableProperty]
    private PageViewModel? _selectedPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading), nameof(DriveSummary))]
    private DriveViewModel? _selectedDrive;

    partial void OnSelectedDriveChanged(DriveViewModel? value) => Reset();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScanning), nameof(IsNotScanning), nameof(HasResults), nameof(IsEmpty), nameof(SafeSummary))]
    [NotifyCanExecuteChangedFor(nameof(ReviewCleanupCommand), nameof(SelectSafeCommand))]
    private ScanState _state;

    public bool IsScanning => State == ScanState.Scanning;

    public bool IsNotScanning => !IsScanning;

    public bool HasResults => State == ScanState.Ready;

    public bool IsEmpty => State == ScanState.NotScanned;

    [ObservableProperty]
    private string _progressText = "";

    /// <summary>A one-line note under the header: scan problems, last cleanup result.</summary>
    [ObservableProperty]
    private string? _notice;

    public string Heading => $"Why is {SelectedDrive?.Letter ?? "this drive"} full?";

    public string DriveSummary => SelectedDrive is { } d
        ? $"{ByteSize.Format(d.UsedBytes)} used of {ByteSize.Format(d.TotalBytes)} · {ByteSize.Format(d.FreeBytes)} free"
        : "No drive selected";

    /// <summary>What Ebb itself can free from findings marked safe.</summary>
    public long SafeBytes => AllFindings.Where(f => f.Safety == Safety.Safe && f.CanSelect).Sum(f => f.Finding.Bytes ?? 0);

    public string SafeSummary => HasResults
        ? $"{ByteSize.Format(SafeBytes)} is safe to remove"
        : "Scan to see what's using space";

    public GridLength UsedLength => Star(Math.Max(0, (SelectedDrive?.UsedBytes ?? 0) - SafeBytes));

    public GridLength SafeLength => Star(SafeBytes);

    public GridLength FreeLength => Star(SelectedDrive?.FreeBytes ?? 1);

    public int SelectedCount => AllFindings.Count(f => f.IsSelected);

    public long SelectedBytes => AllFindings.Where(f => f.IsSelected).Sum(f => f.Finding.Bytes ?? 0);

    public string SelectionText => SelectedCount == 0
        ? "Tick what you'd like to clean. You'll see exactly what happens before anything changes."
        : $"{Display.Count(SelectedCount, "item", "items")} ticked · up to {ByteSize.Format(SelectedBytes)}";

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        if (SelectedDrive is not { } drive)
            return;

        Reset();
        State = ScanState.Scanning;
        ProgressText = "Starting…";
        try
        {
            var progress = new Progress<ScanProgress>(p =>
                ProgressText = $"Scanning… {p.Files:N0} files · {ByteSize.Format(p.Bytes)}");
            var scan = await _services.Scanner.ScanAsync(drive.Root, progress, cancellationToken);

            ProgressText = "Working out what everything is…";
            var report = await _services.Engine.RunAsync(new ScanContext(scan, _services.Paths), cancellationToken);

            Show(report);
            Folders.Load(scan);
            drive.Refresh();
            Notice = scan.InaccessibleFolders > 0
                ? $"Scanned {scan.FileCount:N0} files in {scan.Elapsed.TotalSeconds:0} s. " +
                  $"{scan.InaccessibleFolders:N0} system folders need admin to read, so a little space stays unexplained."
                : $"Scanned {scan.FileCount:N0} files in {scan.Elapsed.TotalSeconds:0} s.";
            State = ScanState.Ready;
        }
        catch (OperationCanceledException)
        {
            State = ScanState.NotScanned;
            Notice = "Scan stopped.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            State = ScanState.NotScanned;
            Notice = $"Scan failed: {ex.Message}";
        }
    }

    private void Show(FindingReport report)
    {
        foreach (var page in FindingPages)
        {
            page.Show(report.Findings
                .Where(f => f.Category == page.Category)
                .Select(f => new FindingViewModel(f, _services.Paths.UserProfile, SelectionChanged,
                    target => _dialogs.Open(target.Target, target.Arguments))));
        }
        RefreshTotals();
    }

    private void Reset()
    {
        foreach (var page in FindingPages)
            page.Clear();
        Folders.Clear();
        State = ScanState.NotScanned;
        Notice = null;
        RefreshTotals();
    }

    private bool CanSelectSafe => HasResults;

    [RelayCommand(CanExecute = nameof(CanSelectSafe))]
    private void SelectSafe()
    {
        foreach (var finding in AllFindings.Where(f => f.CanSelect))
            finding.IsSelected = finding.Safety == Safety.Safe;
    }

    private bool CanReview => HasResults && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanReview))]
    private void ReviewCleanup()
    {
        var selected = AllFindings.Where(f => f.IsSelected).ToList();
        var plan = _services.Planner.Plan(selected.Select(f => f.Finding));
        var cleanup = new CleanupViewModel(plan, _services.Executor, _services.Log.FilePath, _dialogs);
        _dialogs.ShowCleanup(cleanup);

        if (cleanup.Report is { } report)
            AfterCleanup(selected, report);
    }

    private void AfterCleanup(IReadOnlyList<FindingViewModel> selected, CleanupReport report)
    {
        foreach (var result in report.Results.Where(r => r.Outcome == StepOutcome.Done))
        {
            var finding = selected.FirstOrDefault(f => f.Finding == result.Step.Finding);
            if (finding is not null)
                FindingPages.First(p => p.Category == finding.Finding.Category).Remove(finding);
        }
        foreach (var finding in AllFindings)
            finding.IsSelected = false;

        SelectedDrive?.Refresh();
        Notice = report.RecycledBytes > 0
            ? $"Freed {ByteSize.Format(report.FreedBytes)} and moved {ByteSize.Format(report.RecycledBytes)} to the Recycle Bin. Empty it to get that space back."
            : $"Freed {ByteSize.Format(report.FreedBytes)}.";
        OnPropertyChanged(nameof(DriveSummary));
        RefreshTotals();
    }

    [RelayCommand]
    private void OpenLog() => _dialogs.Reveal(_services.Log.FilePath);

    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectionText));
        ReviewCleanupCommand.NotifyCanExecuteChanged();
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(SafeBytes));
        OnPropertyChanged(nameof(SafeSummary));
        OnPropertyChanged(nameof(UsedLength));
        OnPropertyChanged(nameof(SafeLength));
        OnPropertyChanged(nameof(FreeLength));
        SelectionChanged();
    }

    private static GridLength Star(long bytes) => new(Math.Max(bytes, 0) / 1e9 + 1e-6, GridUnitType.Star);
}
