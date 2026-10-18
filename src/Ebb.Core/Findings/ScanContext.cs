using System.Collections.Concurrent;
using Ebb.Core.Platform;
using Ebb.Core.Scanning;

namespace Ebb.Core.Findings;

/// <summary>What detectors look at: the finished scan plus where Windows keeps things.</summary>
public sealed class ScanContext
{
    private static readonly EnumerationOptions TopLevel = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };

    private readonly ConcurrentDictionary<string, long> _sizes = new(PathUtil.Comparer);
    private readonly Lazy<Dictionary<string, LargeFile>> _largeFiles;

    public ScanContext(ScanResult scan, SystemPaths paths, DateTime? nowUtc = null)
    {
        Scan = scan;
        Paths = paths;
        NowUtc = nowUtc ?? DateTime.UtcNow;
        _largeFiles = new(() => scan.LargeFiles.ToDictionary(f => f.Path, PathUtil.Comparer));
    }

    public ScanResult Scan { get; }

    public SystemPaths Paths { get; }

    public DateTime NowUtc { get; }

    public string Root => Scan.RootPath;

    public bool Covers(string path) => Scan.Covers(path);

    /// <summary>Size of a file or folder, or 0 if it's missing or outside the scanned drive.</summary>
    public long SizeOf(string path)
    {
        if (!Covers(path))
            return 0;

        return _sizes.GetOrAdd(PathUtil.Normalize(path), p =>
        {
            if (Scan.Find(p) is { } node)
                return node.Size;
            if (_largeFiles.Value.TryGetValue(p, out var large))
                return large.Size; // also works for files Windows keeps locked, like hiberfil.sys
            return DirectorySizer.Measure(p);
        });
    }

    /// <summary>The files and folders directly inside <paramref name="folder"/>, biggest first. Skips links.</summary>
    public IReadOnlyList<FindingItem> ItemsIn(string folder, Func<FileSystemInfo, bool>? include = null)
    {
        if (!Covers(folder) || !Directory.Exists(folder))
            return [];

        var items = new List<FindingItem>();
        foreach (var info in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", TopLevel))
        {
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || (include is not null && !include(info)))
                continue;
            var bytes = info is FileInfo file ? FileSizes.OnDisk(file) : SizeOf(info.FullName);
            items.Add(new FindingItem(info.FullName, bytes, info.LastWriteTimeUtc));
        }
        return items.OrderByDescending(i => i.Bytes).ToList();
    }
}
