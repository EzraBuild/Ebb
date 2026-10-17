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
        var single = _sandbox.File("single.txt", 10);
        File.SetAttributes(single, FileAttributes.ReadOnly);

        var remover = new ShellFileRemover();
        remover.Remove(_sandbox.PathOf("repo"), RemovalMode.Permanent);
        remover.Remove(single, RemovalMode.Permanent);

        Assert.False(Directory.Exists(_sandbox.PathOf("repo")));
        Assert.False(File.Exists(single));
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
