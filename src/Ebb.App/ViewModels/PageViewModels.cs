using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ebb.Core.Findings;
using Ebb.Core.Scanning;

namespace Ebb.App.ViewModels;

/// <summary>A page in the left-hand list.</summary>
public abstract partial class PageViewModel(string title, string description) : ObservableObject
{
    public string Title { get; } = title;

    /// <summary>The small mono line above the title.</summary>
    public virtual string Eyebrow => Title.ToUpperInvariant();

    public string Description { get; } = description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText))]
    private long? _bytes;

    public string SizeText => Bytes is null ? "" : Display.Size(Bytes);
}

public sealed class FindingsPageViewModel(Category category, string title, string description)
    : PageViewModel(title, description)
{
    public Category Category { get; } = category;

    public ObservableCollection<FindingViewModel> Findings { get; } = [];

    public bool IsEmpty => Findings.Count == 0;

    public override string Eyebrow
    {
        get
        {
            var safe = Findings.Count(f => f.Safety == Safety.Safe);
            var text = Display.Count(Findings.Count, "finding", "findings").ToUpperInvariant();
            return safe > 0 ? $"{text} · {safe} SAFE TO REMOVE" : text;
        }
    }

    public void Show(IEnumerable<FindingViewModel> findings)
    {
        Findings.Clear();
        foreach (var finding in findings)
            Findings.Add(finding);
        Refresh();
    }

    public void Remove(FindingViewModel finding)
    {
        Findings.Remove(finding);
        Refresh();
    }

    public void Clear()
    {
        Findings.Clear();
        Bytes = null;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Eyebrow));
    }

    private void Refresh()
    {
        Bytes = Findings.Sum(f => f.Finding.Bytes ?? 0);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Eyebrow));
    }
}

/// <summary>Plain folder browser over the scan, biggest first, for anything the findings don't explain.</summary>
public sealed partial class FoldersPageViewModel(Action<string> reveal) : PageViewModel("Biggest folders",
    "Everything on the drive, biggest first. Click a folder to look inside. Ebb never removes anything from here.")
{
    private const int MaxRows = 60;

    public override string Eyebrow => "BROWSE";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpCommand))]
    [NotifyPropertyChangedFor(nameof(CurrentPath))]
    private DirNode? _current;

    public string CurrentPath => Current?.FullPath ?? "";

    public ObservableCollection<FolderRowViewModel> Rows { get; } = [];

    public void Load(ScanResult scan)
    {
        Bytes = scan.Root.Size;
        Navigate(scan.Root);
    }

    public void Clear()
    {
        Rows.Clear();
        Current = null;
        Bytes = null;
    }

    public void Navigate(DirNode node)
    {
        Current = node;
        Rows.Clear();
        foreach (var child in node.Children.Take(MaxRows))
            Rows.Add(new FolderRowViewModel(child.Name, child.Size, child.FileCount, node.Size, () => Navigate(child)));

        var filesHere = node.Size - node.Children.Sum(c => c.Size);
        if (filesHere > 0)
            Rows.Add(new FolderRowViewModel("Files and small folders here", filesHere, null, node.Size, null));
    }

    private bool CanGoUp => Current?.Parent is not null;

    [RelayCommand(CanExecute = nameof(CanGoUp))]
    private void Up() => Navigate(Current!.Parent!);

    [RelayCommand]
    private void OpenInExplorer()
    {
        if (Current is not null)
            reveal(Current.FullPath);
    }
}

public sealed partial class FolderRowViewModel(string name, long bytes, long? files, long parentBytes, Action? open)
{
    public const double BarMax = 140;

    public string Name { get; } = name;

    public string SizeText { get; } = Display.Size(bytes);

    public string Meta { get; } = files is { } n ? Display.Count((int)Math.Min(n, int.MaxValue), "file", "files") : "";

    public double BarWidth { get; } = parentBytes > 0 ? Math.Max(1, BarMax * bytes / parentBytes) : 0;

    public bool CanOpen => open is not null;

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open() => open?.Invoke();
}
