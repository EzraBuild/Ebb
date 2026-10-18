using Ebb.Core.Detectors;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.Detectors;

public class DeveloperDetectorTests : IDisposable
{
    private readonly FakeLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    [Fact]
    public async Task Wsl_disk_is_kept_and_offered_for_shrinking()
    {
        var vhdx = _layout.File(@"Users\me\AppData\Local\Packages\Ubuntu\LocalState\ext4.vhdx", 3 * MB);
        var docker = _layout.File(@"Users\me\AppData\Local\Docker\wsl\main\ext4.vhdx", 2 * MB);
        var wsl = new FakeWsl(
            new WslDistro("Ubuntu", vhdx),
            new WslDistro("docker-desktop", docker),
            new WslDistro("Gone", _layout.PathOf(@"missing\ext4.vhdx")));

        var finding = Assert.Single(await Run(new WslDiskDetector(wsl), _layout.Context()));

        Assert.Equal(Safety.Keep, finding.Safety);
        Assert.Equal(3 * MB, finding.Bytes);
        Assert.Equal(new CompactDisk(vhdx, DiskOwner.Wsl, "Ubuntu"), finding.Action);
    }

    [Fact]
    public async Task Docker_disk_and_prune_use_dockers_own_command()
    {
        var disk = _layout.File(@"Users\me\AppData\Local\Docker\wsl\disk\docker_data.vhdx", 4 * MB);
        var runner = new FakeRunner((_, _) => new CommandResult(0,
            """
            {"Type":"Images","Reclaimable":"9GB (90%)"}
            {"Type":"Containers","Reclaimable":"2MB (100%)"}
            {"Type":"Local Volumes","Reclaimable":"5GB (50%)"}
            {"Type":"Build Cache","Reclaimable":"3MB"}
            """, ""));

        var findings = await Run(new DockerDetector(new FakeWsl(), runner, new FakeTools("docker")), _layout.Context());

        var diskFinding = Assert.Single(findings, f => f.Action is CompactDisk);
        Assert.Equal(new CompactDisk(disk, DiskOwner.Docker, null), diskFinding.Action);
        var prune = Assert.Single(findings, f => f.Action is RunTool);
        Assert.Equal("docker system prune -f", ((RunTool)prune.Action!).CommandLine);
        Assert.Equal(5_000_000, prune.Bytes); // containers + build cache only
        Assert.Contains("docker system df --format {{json .}}", runner.Calls);
    }

    [Fact]
    public async Task Docker_prune_size_is_unknown_when_docker_is_not_running()
    {
        var runner = new FakeRunner((_, _) => new CommandResult(1, "", "error during connect"));

        var prune = Assert.Single(await Run(new DockerDetector(new FakeWsl(), runner, new FakeTools("docker")), _layout.Context()));

        Assert.Null(prune.Bytes);
        Assert.Contains("Start Docker Desktop", prune.Reason);
    }

    [Fact]
    public async Task No_docker_prune_without_the_docker_cli()
    {
        var runner = new FakeRunner();

        Assert.Empty(await Run(new DockerDetector(new FakeWsl(), runner, new FakeTools()), _layout.Context()));
        Assert.Empty(runner.Calls);
    }

    [Theory]
    [InlineData("1.37GB (45%)", 1_370_000_000L)]
    [InlineData("512.5MB", 512_500_000L)]
    [InlineData("12kB", 12_000L)]
    [InlineData("0B", 0L)]
    [InlineData("n/a", null)]
    public void Parses_docker_sizes(string text, long? expected) => Assert.Equal(expected, DockerDetector.ParseSize(text));

    [Fact]
    public async Task Node_modules_only_in_real_projects()
    {
        Project(@"src\old-app", daysAgo: 60);
        Project(@"src\fresh-app", daysAgo: 2);
        _layout.File(@"src\no-package-json\node_modules\x\i.js", MB);
        _layout.File(@"Users\me\.vscode\extensions\ext\package.json", 10);
        _layout.File(@"Users\me\.vscode\extensions\ext\node_modules\x\i.js", MB);
        _layout.File(@"Users\me\AppData\Roaming\npm\package.json", 10);
        _layout.File(@"Users\me\AppData\Roaming\npm\node_modules\x\i.js", MB);

        var finding = Assert.Single(await Run(new NodeModulesDetector(), _layout.Context()));

        Assert.Equal(Safety.Safe, finding.Safety);
        Assert.Equal(4 * MB, finding.Bytes);
        Assert.Equal(2, finding.Items.Count);
        Assert.True(finding.Items.Single(i => i.Path.Contains("old-app")).SelectedByDefault);
        Assert.False(finding.Items.Single(i => i.Path.Contains("fresh-app")).SelectedByDefault);
        Assert.Contains("npm install", Assert.IsType<RemoveItems>(finding.Action).RebuiltBy);
    }

    private void Project(string folder, int daysAgo)
    {
        _layout.File($@"{folder}\package.json", 10);
        _layout.File($@"{folder}\node_modules\a\index.js", MB);
        _layout.File($@"{folder}\node_modules\a\node_modules\b\index.js", MB);
        Directory.SetLastWriteTimeUtc(_layout.PathOf(folder), _layout.DaysAgo(daysAgo));
        Directory.SetLastWriteTimeUtc(_layout.PathOf($@"{folder}\node_modules"), _layout.DaysAgo(daysAgo));
    }

    [Fact]
    public async Task Package_caches_use_each_tools_clean_command()
    {
        _layout.File(@"Users\me\AppData\Local\npm-cache\_cacache\blob", 2 * MB);
        _layout.File(@"Users\me\.nuget\packages\newtonsoft.json\x.nupkg", 2 * MB);
        _layout.File(@"Users\me\AppData\Local\NuGet\v3-cache\x.dat", MB);

        var findings = await Run(new PackageCacheDetector(), _layout.Context());

        var npm = Assert.Single(findings, f => f.Id == "dev.cache.npm");
        Assert.Equal("npm cache clean --force", ((RunTool)npm.Action!).CommandLine);
        Assert.Equal(2 * MB, npm.Bytes);
        var nuget = Assert.Single(findings, f => f.Id == "dev.cache.nuget");
        Assert.Equal("dotnet nuget locals all --clear", ((RunTool)nuget.Action!).CommandLine);
        Assert.Equal(3 * MB, nuget.Bytes);
        Assert.Equal(2, nuget.Items.Count); // fallback items if dotnet were missing
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public async Task Keeps_the_newest_sdk_of_each_dotnet_version()
    {
        foreach (var version in new[] { "6.0.100", "8.0.100", "8.0.411", "9.0.100-preview.1", "9.0.100" })
            _layout.File($@"Program Files\dotnet\sdk\{version}\dotnet.dll", MB);
        _layout.Dir(@"Program Files\dotnet\sdk\NuGetFallbackFolder");

        var finding = Assert.Single(await Run(new DotnetSdkDetector(), _layout.Context()));

        Assert.Equal(["8.0.100", "9.0.100-preview.1"], finding.Items.Select(i => Path.GetFileName(i.Path)));
        Assert.Equal(Safety.CheckFirst, finding.Safety);
        Assert.IsType<OpenTarget>(finding.Action);
        Assert.All(finding.Items, i => Assert.False(i.SelectedByDefault));
    }

    [Fact]
    public async Task Visual_studio_caches_and_installer_cache()
    {
        _layout.File(@"Users\me\AppData\Local\Microsoft\VisualStudio\17.0_abc\ComponentModelCache\cache.dat", MB);
        _layout.File(@"ProgramData\Microsoft\VisualStudio\Packages\pkg\setup.cab", 2 * MB);
        _layout.File(@"ProgramData\Package Cache\{guid}\setup.msi", 3 * MB);

        var findings = await Run(new VisualStudioDetector(), _layout.Context());

        var caches = Assert.Single(findings, f => f.Id == "dev.vs.component-cache");
        Assert.IsType<RemoveItems>(caches.Action);
        Assert.EndsWith("ComponentModelCache", Assert.Single(caches.Items).Path);
        Assert.Equal(Safety.CheckFirst, Assert.Single(findings, f => f.Id == "dev.vs.download-cache").Safety);
        var packageCache = Assert.Single(findings, f => f.Id == "win.package-cache");
        Assert.Equal(Safety.Keep, packageCache.Safety);
        Assert.Null(packageCache.Action);
    }
}
