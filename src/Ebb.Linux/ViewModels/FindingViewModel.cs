using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ebb.Core.Findings;

namespace Ebb.Linux.ViewModels;

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

    // SAFE is filled, CHECK FIRST is outlined, KEEP is dashed: three looks that work without color.
    public bool IsSafe => Finding.Safety == Safety.Safe;

    public bool IsCheckFirst => Finding.Safety == Safety.CheckFirst;

    public bool IsKeep => Finding.Safety == Safety.Keep;

    public string SizeText => Display.Size(Finding.Bytes);

    public string Meta { get; }

    /// <summary>Ebb can do this one itself (anything except "open a folder or app").</summary>
    public bool CanSelect => Finding.Action is not null and not OpenTarget;

    public bool CanOpen => Finding.Action is OpenTarget;

    public string? OpenLabel => (Finding.Action as OpenTarget)?.Label;

    public string ActionText => Finding.Action switch
    {
        RemoveItems { RebuiltBy: not null } =>
            $"Deletes {Display.Count(Finding.Items.Count, "item", "items")} for good, skipping the Trash. " +
            "You can untick any before it starts.",
        RemoveItems => Finding.Items.Count > 1
            ? $"Removes {Display.Count(Finding.Items.Count, "item", "items")}. You can untick any before it starts."
            : "Removes it. You'll see it before it starts.",
        RunTool { AsRoot: true } tool => $"Runs: {tool.CommandLine} (asks for your password)",
        RunTool tool => $"Runs: {tool.CommandLine}",
        EmptyRecycleBin => "Empties the Trash",
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
