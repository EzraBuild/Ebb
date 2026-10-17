using Ebb.Core.Scanning;

namespace Ebb.Tests.Scanning;

public class DriveScannerTests : IDisposable
{
    private const int MB = 1024 * 1024;
    private readonly TestSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    private static DriveScanner Scanner(long largeFile = 5 * MB) =>
        new(new ScanOptions { LargeFileThreshold = largeFile, PruneBelow = 0 });

    [Fact]
    public void Adds_up_sizes_through_the_tree()
    {
        _sandbox.File(@"a\one.bin", 3 * MB);
        _sandbox.File(@"a\b\two.bin", 2 * MB);
        _sandbox.File(@"c\three.bin", 1 * MB);

        var result = Scanner().Scan(_sandbox.Root);

        Assert.Equal(6 * MB, result.Root.Size);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(5 * MB, result.Find(_sandbox.PathOf("a"))!.Size);
        Assert.Equal(2 * MB, result.Find(_sandbox.PathOf(@"a\b"))!.Size);
        Assert.Equal(1 * MB, result.Find(_sandbox.PathOf("c"))!.Size);
    }

    [Fact]
    public void Children_are_sorted_biggest_first()
    {
        _sandbox.File(@"small\f.bin", 1 * MB);
        _sandbox.File(@"big\f.bin", 4 * MB);
        _sandbox.File(@"mid\f.bin", 2 * MB);

        var result = Scanner().Scan(_sandbox.Root);

        Assert.Equal(["big", "mid", "small"], result.Root.Children.Select(c => c.Name));
    }

    [Fact]
    public void Remembers_large_files()
    {
        _sandbox.File(@"videos\movie.mp4", 6 * MB);
        _sandbox.File(@"videos\clip.mp4", 1 * MB);

        var result = Scanner(largeFile: 5 * MB).Scan(_sandbox.Root);

        var large = Assert.Single(result.LargeFiles);
        Assert.Equal(_sandbox.PathOf(@"videos\movie.mp4"), large.Path);
        Assert.Equal(6 * MB, large.Size);
    }

    [Fact]
    public void Does_not_follow_junctions()
    {
        _sandbox.File(@"real\data.bin", 2 * MB);
        _sandbox.Junction("link", _sandbox.PathOf("real"));

        var result = Scanner().Scan(_sandbox.Root);

        Assert.Equal(2 * MB, result.Root.Size);
        Assert.Null(result.Find(_sandbox.PathOf("link")));
    }

    [Fact]
    public void Prunes_small_folders_but_keeps_their_size_in_the_parent()
    {
        _sandbox.File(@"parent\tiny\f.bin", 1000);
        _sandbox.File(@"parent\big\f.bin", 2 * MB);

        var result = new DriveScanner(new ScanOptions { PruneBelow = MB }).Scan(_sandbox.Root);

        var parent = result.Find(_sandbox.PathOf("parent"))!;
        Assert.Equal(2 * MB + 1000, parent.Size);
        Assert.Equal(["big"], parent.Children.Select(c => c.Name));
    }

    [Fact]
    public void Finds_folders_by_name_without_descending_into_matches()
    {
        _sandbox.File(@"app1\node_modules\x\f.bin", 2 * MB);
        _sandbox.File(@"app1\node_modules\y\node_modules\f.bin", 2 * MB);
        _sandbox.File(@"deep\app2\node_modules\f.bin", 2 * MB);

        var result = Scanner().Scan(_sandbox.Root);

        var found = result.FindDirectories("node_modules").Select(m => m.Path).Order().ToList();
        Assert.Equal(
            [_sandbox.PathOf(@"app1\node_modules"), _sandbox.PathOf(@"deep\app2\node_modules")],
            found);
        Assert.Equal(4 * MB, result.Find(found[0])!.Size);
    }

    [Fact]
    public void Find_returns_null_outside_the_scan()
    {
        _sandbox.File(@"a\f.bin", 10);

        var result = Scanner().Scan(_sandbox.PathOf("a"));

        Assert.Null(result.Find(_sandbox.Root));
        Assert.Same(result.Root, result.Find(_sandbox.PathOf("a")));
    }

    [Fact]
    public void Can_be_cancelled()
    {
        _sandbox.File(@"a\f.bin", 10);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => Scanner().Scan(_sandbox.Root, null, cancel.Token));
    }

    [Fact]
    public void Reports_progress()
    {
        _sandbox.File(@"a\f.bin", MB);
        var reports = new List<ScanProgress>();

        Scanner().Scan(_sandbox.Root, new SyncProgress<ScanProgress>(reports.Add));

        Assert.Equal(MB, reports[^1].Bytes);
        Assert.Equal(1, reports[^1].Files);
    }

    [Fact]
    public void Measures_a_single_folder_directly()
    {
        _sandbox.File(@"a\one.bin", 3 * MB);
        _sandbox.File(@"a\b\two.bin", 2 * MB);
        _sandbox.Junction(@"a\link", _sandbox.PathOf("a"));

        Assert.Equal(5 * MB, DirectorySizer.Measure(_sandbox.PathOf("a")));
        Assert.Equal(3 * MB, DirectorySizer.Measure(_sandbox.PathOf(@"a\one.bin")));
        Assert.Equal(0, DirectorySizer.Measure(_sandbox.PathOf("missing")));
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            lock (this)
                report(value);
        }
    }
}
