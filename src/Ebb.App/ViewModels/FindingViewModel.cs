using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ebb.Core.Elevation;
using Ebb.Core.Findings;

namespace Ebb.App.ViewModels;

/// <summary>One row in a findings list.</summary>
public sealed partial class FindingViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private readonly Action<OpenTarget> _open;

    public FindingViewModel(Finding finding, string userProfile, Action selectionChanged, Action<OpenTarget> open)
    {
        Finding = finding;
        _selectionChanged = selectionChanged;
        _open = open;

        Meta = finding.Path is { } path
            ? Display.ShortPath(path, userProfile)
            : Display.Count(finding.Items.Count, "item", "items");
        if (finding.Path is not null && finding.Items.Count > 1)
            Meta = $"{Display.Count(finding.Items.Count, "item", "items")} in {Meta}";
    }

    public Finding Finding { get; }

    public string Title => Finding.Title;

    public string Reason => Finding.Reason;

    public Safety Safety => Finding.Safety;

    public string SafetyText => Display.SafetyLabel(Finding.Safety);

    public string SizeText => Display.Size(Finding.Bytes);

    public string Meta { get; }

    /// <summary>Ebb can do this one itself (anything except "open a Windows tool").</summary>
    public bool CanSelect => Finding.Action is not null and not OpenTarget;

    public bool CanOpen => Finding.Action is OpenTarget;

    public string? OpenLabel => (Finding.Action as OpenTarget)?.Label;

    public string ActionText => Finding.Action switch
    {
        RemoveItems { RebuiltBy: not null } =>
            $"Deletes {Display.Count(Finding.Items.Count, "item", "items")} for good, skipping the Recycle Bin. " +
            "You can untick any before it starts.",
        RemoveItems => Finding.Items.Count > 1
            ? $"Removes {Display.Count(Finding.Items.Count, "item", "items")}. You can untick any before it starts."
            : "Removes it. You'll see it before it starts.",
        RunTool tool => $"Runs: {tool.CommandLine}",
        RunElevated elevated => $"Runs: {HelperCommands.Describe(elevated.Operation)} (asks for admin)",
        EmptyRecycleBin => "Empties the Recycle Bin",
        CompactDisk => "Shrinks the disk file (asks for admin)",
        _ => "",
    };

    public bool HasActionText => ActionText.Length > 0;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();

    [RelayCommand]
    private void Open()
    {
        if (Finding.Action is OpenTarget target)
            _open(target);
    }
}
