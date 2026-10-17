using Ebb.Core.Elevation;
using Ebb.Core.Platform;
using Ebb.Core.Vhdx;

namespace Ebb.Tests.Vhdx;

public class HelperArgsTests
{
    private const string Result = @"C:\Temp\Ebb\ebb-helper-123.json";

    [Theory]
    [InlineData(HelperOperation.HibernateOff, null)]
    [InlineData(HelperOperation.ComponentCleanup, null)]
    [InlineData(HelperOperation.CompactVhdx, @"C:\wsl\ext4.vhdx")]
    public void Round_trips_requests(HelperOperation operation, string? path)
    {
        var args = HelperArgs.Build(new HelperRequest(operation, path), Result).ToArray();

        Assert.True(HelperArgs.TryParse(args, out var request, out var resultPath, out _));
        Assert.Equal(new HelperRequest(operation, path), request);
        Assert.Equal(Result, resultPath);
    }

    [Theory]
    [InlineData("format-c", "--result", Result)]
    [InlineData("compact-vhdx", "--result", Result)]
    [InlineData("hibernate-off", "--result", @"C:\Windows\System32\evil.dll")]
    [InlineData("hibernate-off", "--result", "ebb-helper-relative.json")]
    [InlineData("hibernate-off")]
    public void Rejects_anything_else(params string[] args) =>
        Assert.False(HelperArgs.TryParse(args, out _, out _, out _));
}

public class HelperOperationsTests : IDisposable
{
    private readonly TestSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public async Task Turns_hibernation_off_with_powercfg_and_measures_free_space()
    {
        var runner = new FakeRunner();
        var free = new Queue<long>([1000, 7000]);

        var result = await new HelperOperations(runner, free.Dequeue).RunAsync(new HelperRequest(HelperOperation.HibernateOff));

        Assert.Equal(["powercfg /hibernate off"], runner.Calls);
        Assert.True(result.Succeeded);
        Assert.Equal(6000, result.FreedBytes);
    }

    [Fact]
    public async Task Runs_dism_component_cleanup()
    {
        var runner = new FakeRunner();

        await new HelperOperations(runner, () => 0).RunAsync(new HelperRequest(HelperOperation.ComponentCleanup));

        Assert.Equal(["DISM /Online /Cleanup-Image /StartComponentCleanup"], runner.Calls);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\config\SYSTEM")]
    [InlineData(@"relative\disk.vhdx")]
    [InlineData(@"C:\does\not\exist.vhdx")]
    public async Task Only_shrinks_existing_vhdx_files(string path)
    {
        var runner = new FakeRunner();

        var result = await new HelperOperations(runner).RunAsync(new HelperRequest(HelperOperation.CompactVhdx, path));

        Assert.False(result.Succeeded);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Compacts_with_diskpart_when_optimize_vhd_is_missing()
    {
        var vhdx = _sandbox.File("ext4.vhdx", 10_000);
        string? script = null;
        var runner = new FakeRunner((tool, args) =>
        {
            if (tool == "powershell.exe")
                return new CommandResult(1, "", "Optimize-VHD not found");
            script = File.ReadAllText(args[1]);
            using (var stream = new FileStream(vhdx, FileMode.Open))
                stream.SetLength(4_000); // what compacting would do
            return new CommandResult(0, "DiskPart successfully compacted the virtual disk file.", "");
        });

        var result = await new HelperOperations(runner).RunAsync(new HelperRequest(HelperOperation.CompactVhdx, vhdx));

        Assert.True(result.Succeeded);
        Assert.Equal(6_000, result.FreedBytes);
        Assert.Equal(DiskpartScript.Compact(vhdx), script);
        Assert.Contains("attach vdisk readonly", script);
        Assert.StartsWith("diskpart /s", runner.Calls[^1]);
    }

    [Fact]
    public async Task Uses_optimize_vhd_when_available()
    {
        var vhdx = _sandbox.File("it's.vhdx", 100);
        var runner = new FakeRunner();

        await new HelperOperations(runner).RunAsync(new HelperRequest(HelperOperation.CompactVhdx, vhdx));

        Assert.Equal(2, runner.Calls.Count);
        Assert.Contains($"Optimize-VHD -LiteralPath '{vhdx.Replace("'", "''")}' -Mode Full", runner.Calls[1]);
    }

    [Fact]
    public async Task Detaches_the_disk_if_compacting_fails()
    {
        var vhdx = _sandbox.File("ext4.vhdx", 100);
        var scripts = new List<string>();
        var runner = new FakeRunner((tool, args) =>
        {
            if (tool == "powershell.exe")
                return new CommandResult(1, "", "");
            scripts.Add(File.ReadAllText(args[1]));
            return new CommandResult(scripts.Count == 1 ? 1 : 0, "", "Virtual Disk Service error: the file is in use.");
        });

        var result = await new HelperOperations(runner).RunAsync(new HelperRequest(HelperOperation.CompactVhdx, vhdx));

        Assert.False(result.Succeeded);
        Assert.Contains("in use", result.Message);
        Assert.Equal(DiskpartScript.Detach(vhdx), scripts[1]);
    }
}
