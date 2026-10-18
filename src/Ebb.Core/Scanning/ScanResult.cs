namespace Ebb.Core.Scanning;

public sealed class ScanResult
{
    private readonly Func<string, bool>? _skipped;

    internal ScanResult(DirNode root, IReadOnlyList<LargeFile> largeFiles, long fileCount, long folderCount,
        long inaccessibleFolders, TimeSpan elapsed, Func<string, bool>? skipped = null)
    {
        _skipped = skipped;
        Root = root;
        LargeFiles = largeFiles;
        FileCount = fileCount;
        FolderCount = folderCount;
        InaccessibleFolders = inaccessibleFolders;
        Elapsed = elapsed;
    }

    public DirNode Root { get; }

    public string RootPath => Root.Name;

    /// <summary>Big files found anywhere in the scan, biggest first.</summary>
    public IReadOnlyList<LargeFile> LargeFiles { get; }

    public long FileCount { get; }

    public long FolderCount { get; }

    /// <summary>Folders Windows wouldn't let us read (usually system folders that need admin).</summary>
    public long InaccessibleFolders { get; }

    public TimeSpan Elapsed { get; }

    /// <summary>True for paths on the scanned drive, and not in a folder the scan skipped (like another disk).</summary>
    public bool Covers(string path)
    {
        if (!PathUtil.IsSameOrUnder(path, RootPath))
            return false;
        if (_skipped is null)
            return true;

        var folder = RootPath;
        foreach (var segment in PathUtil.RelativeSegments(path, RootPath))
        {
            folder = Path.Join(folder, segment);
            if (_skipped(folder))
                return false;
        }
        return true;
    }

    /// <summary>The node for a folder, or null if it's outside the scan or was too small to keep.</summary>
    public DirNode? Find(string path)
    {
        if (!Covers(path))
            return null;

        var node = Root;
        foreach (var segment in PathUtil.RelativeSegments(path, RootPath))
        {
            node = node.Child(segment);
            if (node is null)
                return null;
        }
        return node;
    }

    /// <summary>Every folder with this name, without looking inside a match (so nested node_modules count once).</summary>
    public IEnumerable<(string Path, DirNode Node)> FindDirectories(string name)
    {
        var stack = new Stack<(string Path, DirNode Node)>();
        stack.Push((RootPath, Root));
        while (stack.Count > 0)
        {
            var (path, node) = stack.Pop();
            foreach (var child in node.Children)
            {
                var childPath = Path.Join(path, child.Name);
                if (string.Equals(child.Name, name, PathUtil.Comparison))
                    yield return (childPath, child);
                else
                    stack.Push((childPath, child));
            }
        }
    }
}
