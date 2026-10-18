using CommunityToolkit.Mvvm.ComponentModel;
using Ebb.Core;

namespace Ebb.App.ViewModels;

/// <summary>A folder that holds several items in the plan, so they can be ticked or unticked together.</summary>
public sealed partial class FolderGroupViewModel : ObservableObject
{
    private readonly IReadOnlyList<PlanItemViewModel> _items;

    private FolderGroupViewModel(string path, IReadOnlyList<PlanItemViewModel> items)
    {
        Path = path;
        _items = items;
    }

    public string Path { get; }

    public int Count => _items.Count;

    public string Summary => $"{Display.Count(_items.Count, "item", "items")} · {ByteSize.Format(_items.Sum(i => i.Bytes))}";

    /// <summary>True when everything in it is ticked, false when nothing is, null when some are.</summary>
    public bool? Selected
    {
        get
        {
            var ticked = _items.Count(i => i.Selected);
            return ticked == _items.Count ? true : ticked == 0 ? false : null;
        }
        set
        {
            foreach (var item in _items)
                item.Selected = value ?? true;
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(Selected));

    /// <summary>
    /// The folders above two or more items. A folder holding the same items as a folder inside it is left out
    /// (the inner one says more), and so is one holding everything (that's just "tick all").
    /// </summary>
    public static IReadOnlyList<FolderGroupViewModel> Build(IEnumerable<PlanItemViewModel> items)
    {
        var selectable = items.Where(i => i.CanSelect).ToList();
        var byFolder = new Dictionary<string, List<PlanItemViewModel>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in selectable)
        {
            for (var folder = System.IO.Path.GetDirectoryName(item.Path); folder is not null; folder = System.IO.Path.GetDirectoryName(folder))
            {
                if (!byFolder.TryGetValue(folder, out var list))
                    byFolder[folder] = list = [];
                list.Add(item);
            }
        }

        var sameAsInner = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (folder, list) in byFolder)
        {
            if (System.IO.Path.GetDirectoryName(folder) is { } parent && byFolder[parent].Count == list.Count)
                sameAsInner.Add(parent);
        }

        return byFolder
            .Where(f => f.Value.Count >= 2 && f.Value.Count < selectable.Count && !sameAsInner.Contains(f.Key))
            .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .Select(f => new FolderGroupViewModel(f.Key, f.Value))
            .ToList();
    }
}
