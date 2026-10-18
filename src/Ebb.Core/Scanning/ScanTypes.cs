namespace Ebb.Core.Scanning;

public sealed record ScanOptions
{
    /// <summary>Files at least this big are remembered individually (videos, disk images, ...).</summary>
    public long LargeFileThreshold { get; init; } = 100L * 1024 * 1024;

    /// <summary>Folders smaller than this are dropped from the tree after the scan to save memory.</summary>
    public long PruneBelow { get; init; } = 1024 * 1024;

    public int MaxParallelism { get; init; } = Math.Clamp(Environment.ProcessorCount, 8, 16);

    /// <summary>
    /// Folders not to go into, by full path. On Linux: other drives mounted inside this one, and
    /// /proc-style system file systems that take no disk space.
    /// </summary>
    public Func<string, bool>? SkipFolder { get; init; }
}

public readonly record struct ScanProgress(long Files, long Folders, long Bytes);

public sealed record LargeFile(string Path, long Size, DateTime LastWriteUtc);
