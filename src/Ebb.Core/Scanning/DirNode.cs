namespace Ebb.Core.Scanning;

/// <summary>A folder in the scan tree. <see cref="Size"/> includes everything below it.</summary>
public sealed class DirNode
{
    private List<DirNode>? _children;

    internal DirNode(string name, DirNode? parent)
    {
        Name = name;
        Parent = parent;
    }

    /// <summary>Folder name, or the full root path for the root node.</summary>
    public string Name { get; }

    public DirNode? Parent { get; }

    public long Size { get; internal set; }

    public long FileCount { get; internal set; }

    /// <summary>Sub-folders, biggest first. Tiny ones are pruned after the scan.</summary>
    public IReadOnlyList<DirNode> Children => (IReadOnlyList<DirNode>?)_children ?? [];

    public string FullPath => Parent is null ? Name : Path.Join(Parent.FullPath, Name);

    internal List<DirNode>? ChildList => _children;

    internal void SetChildren(List<DirNode>? children) => _children = children;

    public DirNode? Child(string name)
    {
        if (_children is null)
            return null;
        foreach (var child in _children)
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return null;
    }
}
