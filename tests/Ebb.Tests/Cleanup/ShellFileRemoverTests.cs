using Ebb.Core.Cleanup;

namespace Ebb.Tests.Cleanup;

public class ShellFileRemoverTests : IDisposable
{
    private readonly TestSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void Permanently_deletes_read_only_files_and_folders()
    {
        var file = _sandbox.File(@"repo\.git\objects\ab\cdef", 10);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        var folder = _sandbox.Dir(@"repo\.git\objects\ro");
        File.SetAttributes(folder, FileAttributes.Directory | FileAttributes.ReadOnly);
        var single = _sandbox.File("single.txt", 10);
        File.SetAttributes(single, FileAttributes.ReadOnly);

        var remover = new ShellFileRemover();
        remover.Remove(_sandbox.PathOf("repo"), RemovalMode.Permanent);
        remover.Remove(single, RemovalMode.Permanent);

        Assert.False(Directory.Exists(_sandbox.PathOf("repo")));
        Assert.False(File.Exists(single));
    }

    [Fact]
    public void Permanently_deletes_big_trees_and_leaves_nothing_behind()
    {
        for (var i = 0; i < 300; i++)
            _sandbox.File($@"app\node_modules\pkg{i % 30}\lib\f{i}.js", 100);

        new ShellFileRemover().Remove(_sandbox.PathOf(@"app\node_modules"), RemovalMode.Permanent);

        Assert.Empty(Directory.GetFileSystemEntries(_sandbox.PathOf("app")));
    }

    [Fact]
    public void Removes_links_inside_but_never_what_they_point_to()
    {
        var store = _sandbox.File(@"store\pkg\index.js", 10);
        _sandbox.File(@"app\node_modules\.pnpm\a\index.js", 10);
        _sandbox.Junction(@"app\node_modules\pkg", _sandbox.PathOf(@"store\pkg"));

        new ShellFileRemover().Remove(_sandbox.PathOf(@"app\node_modules"), RemovalMode.Permanent);

        Assert.Empty(Directory.GetFileSystemEntries(_sandbox.PathOf("app")));
        Assert.True(File.Exists(store));
    }

    [Fact]
    public void Deletes_nothing_from_a_folder_that_is_in_use()
    {
        var open = _sandbox.File(@"app\node_modules\a\index.js", 10);
        var other = _sandbox.File(@"app\node_modules\b\index.js", 10);

        using (new FileStream(open, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var error = Assert.Throws<IOException>(() =>
                new ShellFileRemover().Remove(_sandbox.PathOf(@"app\node_modules"), RemovalMode.Permanent));
            Assert.Contains("in use", error.Message);
        }

        Assert.True(File.Exists(open));
        Assert.True(File.Exists(other));
        Assert.Single(Directory.GetFileSystemEntries(_sandbox.PathOf("app")));
    }

    [Fact]
    public void Shell_delete_call_works_without_using_the_recycle_bin()
    {
        var folder = _sandbox.File(@"shell\nested\file.txt", 10);

        ShellFileRemover.ShellDelete(_sandbox.PathOf("shell"), toRecycleBin: false);

        Assert.False(Directory.Exists(_sandbox.PathOf("shell")));
        Assert.Throws<IOException>(() => ShellFileRemover.ShellDelete(_sandbox.PathOf("missing"), toRecycleBin: false));
    }

    /// <summary>Opt-in: puts one tiny test file from %TEMP% into your real Recycle Bin.</summary>
    [RecycleBinFact]
    public void Moves_a_file_to_the_recycle_bin()
    {
        var file = _sandbox.File($"ebb-recycle-test-{Guid.NewGuid():N}.txt", 10);

        new ShellFileRemover().Remove(file, RemovalMode.RecycleBin);

        Assert.False(File.Exists(file));
    }
}

public sealed class RecycleBinFactAttribute : FactAttribute
{
    public RecycleBinFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EBB_RECYCLE_TEST") != "1")
            Skip = "Moves a temp test file into your real Recycle Bin. Set EBB_RECYCLE_TEST=1 to run it.";
    }
}
