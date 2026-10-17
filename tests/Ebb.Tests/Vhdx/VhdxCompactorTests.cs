using Ebb.Core.Elevation;
using Ebb.Core.Platform;
using Ebb.Core.Vhdx;

namespace Ebb.Tests.Vhdx;

public class VhdxCompactorTests : IDisposable
{
    private readonly TestSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    private sealed class FakeProcesses(params string[] running) : IProcessInspector
    {
        public bool IsRunning(string processName) => running.Contains(processName);
    }

    [Fact]
    public async Task Trims_shuts_down_wsl_then_compacts_with_admin()
    {
        var vhdx = _sandbox.File("ext4.vhdx", 1000);
        var runner = new FakeRunner();
        var elevated = new FakeElevated();

        var result = await new VhdxCompactor(runner, elevated, new FakeProcesses())
            .CompactAsync(new(vhdx, Ebb.Core.Findings.DiskOwner.Wsl, "Ubuntu"));

        Assert.True(result.Succeeded);
        Assert.Equal(["wsl -d Ubuntu -u root -e fstrim -a", "wsl --shutdown"], runner.Calls);
        Assert.Equal([new HelperRequest(HelperOperation.CompactVhdx, vhdx)], elevated.Requests);
    }

    [Fact]
    public async Task Waits_for_docker_desktop_to_be_closed()
    {
        var vhdx = _sandbox.File("docker_data.vhdx", 1000);
        var runner = new FakeRunner();
        var elevated = new FakeElevated();

        var result = await new VhdxCompactor(runner, elevated, new FakeProcesses("Docker Desktop"))
            .CompactAsync(new(vhdx, Ebb.Core.Findings.DiskOwner.Docker, null));

        Assert.False(result.Succeeded);
        Assert.Contains("Quit Docker Desktop", result.Message);
        Assert.Empty(runner.Calls);
        Assert.Empty(elevated.Requests);
    }

    [Fact]
    public async Task Stops_if_wsl_will_not_shut_down()
    {
        var vhdx = _sandbox.File("docker_data.vhdx", 1000);
        var runner = new FakeRunner((_, _) => new CommandResult(1, "", "WSL is busy"));
        var elevated = new FakeElevated();

        var result = await new VhdxCompactor(runner, elevated, new FakeProcesses())
            .CompactAsync(new(vhdx, Ebb.Core.Findings.DiskOwner.Docker, null));

        Assert.False(result.Succeeded);
        Assert.Empty(elevated.Requests);
    }
}
