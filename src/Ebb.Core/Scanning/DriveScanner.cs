using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Security;

namespace Ebb.Core.Scanning;

/// <summary>
/// Walks a folder tree with several threads and adds up sizes. Read-only: it never opens files,
/// skips links and junctions so nothing is counted twice, and keeps going past folders it can't read.
/// </summary>
public sealed class DriveScanner
{
    private readonly ScanOptions _options;

    public DriveScanner(ScanOptions? options = null) => _options = options ?? new ScanOptions();

    public Task<ScanResult> ScanAsync(string root, IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(root, progress, cancellationToken), cancellationToken);

    public ScanResult Scan(string root, IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var rootPath = PathUtil.Normalize(root);
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Folder not found: {rootPath}");

        var stopwatch = Stopwatch.StartNew();
        var run = new Run(_options, cancellationToken);
        var rootNode = new DirNode(rootPath, null);

        using (progress is null ? null : new Timer(_ => progress.Report(run.Progress), null, 100, 100))
            run.Execute(rootNode);

        Finish(rootNode, _options.PruneBelow);
        progress?.Report(run.Progress);

        var largeFiles = run.LargeFiles.OrderByDescending(f => f.Size).ToList();
        return new ScanResult(rootNode, largeFiles, run.Files, run.Folders, run.Inaccessible, stopwatch.Elapsed);
    }

    /// <summary>Adds child sizes into parents, then drops tiny folders and sorts the rest biggest first.</summary>
    private static void Finish(DirNode root, long pruneBelow)
    {
        var stack = new Stack<(DirNode Node, bool ChildrenDone)>();
        stack.Push((root, false));
        while (stack.Count > 0)
        {
            var (node, childrenDone) = stack.Pop();
            var children = node.ChildList;
            if (children is null)
                continue;

            if (!childrenDone)
            {
                stack.Push((node, true));
                foreach (var child in children)
                    stack.Push((child, false));
                continue;
            }

            foreach (var child in children)
            {
                node.Size += child.Size;
                node.FileCount += child.FileCount;
            }
            children.RemoveAll(c => c.Size < pruneBelow);
            children.Sort((a, b) => b.Size.CompareTo(a.Size));
            children.TrimExcess();
            node.SetChildren(children.Count == 0 ? null : children);
        }
    }

    private sealed class Run
    {
        private static readonly EnumerationOptions EnumerationOptions = new()
        {
            IgnoreInaccessible = false, // we want to count what we couldn't read
            AttributesToSkip = 0,       // hidden and system files take space too
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            BufferSize = 32 * 1024,
        };

        private readonly ScanOptions _options;
        private readonly CancellationToken _cancel;
        private readonly ConcurrentQueue<(DirNode Node, string Path)> _queue = new();
        private readonly SemaphoreSlim _ready = new(0);
        private int _pending;
        private long _files, _folders, _bytes, _inaccessible;

        public Run(ScanOptions options, CancellationToken cancel)
        {
            _options = options;
            _cancel = cancel;
        }

        public ConcurrentBag<LargeFile> LargeFiles { get; } = [];

        public long Files => Interlocked.Read(ref _files);

        public long Folders => Interlocked.Read(ref _folders);

        public long Inaccessible => Interlocked.Read(ref _inaccessible);

        public ScanProgress Progress => new(Files, Folders, Interlocked.Read(ref _bytes));

        public void Execute(DirNode root)
        {
            _pending = 1;
            _queue.Enqueue((root, root.Name));
            _ready.Release();

            var workers = Enumerable.Range(0, _options.MaxParallelism)
                .Select(_ => Task.Factory.StartNew(Work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                .ToArray();
            try
            {
                Task.WaitAll(workers);
            }
            catch (AggregateException) when (_cancel.IsCancellationRequested)
            {
                throw new OperationCanceledException(_cancel);
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
            {
                throw ex.InnerExceptions[0];
            }
        }

        private void Work()
        {
            while (true)
            {
                _ready.Wait(_cancel);
                if (!_queue.TryDequeue(out var item))
                    return; // woken up because the scan is done

                Process(item.Node, item.Path);

                if (Interlocked.Decrement(ref _pending) == 0)
                    _ready.Release(_options.MaxParallelism);
            }
        }

        private void Process(DirNode node, string path)
        {
            var skip = _options.SkipFolder;
            long bytes = 0, files = 0;
            List<DirNode>? children = null;
            try
            {
                var entries = new FileSystemEnumerable<DirNode?>(path, (ref FileSystemEntry entry) =>
                {
                    if (entry.IsDirectory)
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                            return null;
                        var name = entry.FileName.ToString();
                        return skip is not null && skip(Path.Join(path, name)) ? null : new DirNode(name, node);
                    }

                    var size = FileSizes.OnDisk(ref entry);
                    bytes += size;
                    files++;
                    if (size >= _options.LargeFileThreshold)
                        LargeFiles.Add(new LargeFile(entry.ToFullPath(), size, entry.LastWriteTimeUtc.UtcDateTime));
                    return null;
                }, EnumerationOptions);

                foreach (var child in entries)
                {
                    if (child is not null)
                        (children ??= []).Add(child);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
            {
                Interlocked.Increment(ref _inaccessible);
            }

            node.Size = bytes;
            node.FileCount = files;
            node.SetChildren(children);
            Interlocked.Add(ref _files, files);
            Interlocked.Add(ref _bytes, bytes);
            Interlocked.Increment(ref _folders);

            if (children is null)
                return;

            Interlocked.Add(ref _pending, children.Count);
            foreach (var child in children)
                _queue.Enqueue((child, Path.Join(path, child.Name)));
            _ready.Release(children.Count);
        }
    }
}
