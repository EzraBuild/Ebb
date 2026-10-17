using Ebb.Core.Detectors;
using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.Detectors;

public class WindowsDetectorTests : IDisposable
{
    private readonly FakeLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    [Fact]
    public async Task Temp_files_only_include_items_older_than_a_day()
    {
        _layout.File(@"Users\me\AppData\Local\Temp\old.tmp", MB, _layout.DaysAgo(3));
        _layout.File(@"Users\me\AppData\Local\Temp\new.tmp", MB, _layout.Now);
        _layout.File(@"Users\me\AppData\Local\Temp\oldDir\a.bin", MB);
        Directory.SetLastWriteTimeUtc(_layout.PathOf(@"Users\me\AppData\Local\Temp\oldDir"), _layout.DaysAgo(5));
        _layout.File(@"Windows\Temp\system.tmp", MB);

        var findings = await Run(new TempFilesDetector(), _layout.Context());

        var temp = Assert.Single(findings, f => f.Id == "win.temp");
        Assert.Equal(["old.tmp", "oldDir"], temp.Items.Select(i => Path.GetFileName(i.Path)).Order());
        Assert.IsType<RemoveItems>(temp.Action);
        // Windows' own temp folder is only ever handed to Disk Cleanup.
        var windowsTemp = Assert.Single(findings, f => f.Id == "win.windows-temp");
        Assert.Equal("cleanmgr.exe", ((OpenTarget)windowsTemp.Action!).Target);
    }

    [Fact]
    public async Task Update_leftovers_go_through_windows_tools()
    {
        _layout.File(@"Windows\SoftwareDistribution\Download\update.cab", MB);
        _layout.File(@"$WINDOWS.~BT\Sources\install.esd", 2 * MB);
        _layout.File(@"Windows.old\Windows\explorer.exe", 3 * MB);
        _layout.Dir(@"Windows\WinSxS");

        var findings = await Run(new WindowsUpdateDetector(), _layout.Context());

        Assert.Equal(4, findings.Count);
        Assert.All(findings.Where(f => f.Id != "win.component-store"), f => Assert.IsType<OpenTarget>(f.Action));
        var store = Assert.Single(findings, f => f.Id == "win.component-store");
        Assert.Equal(new RunElevated(HelperOperation.ComponentCleanup), store.Action);
        Assert.Null(store.Bytes);
        Assert.Equal(Safety.CheckFirst, Assert.Single(findings, f => f.Id == "win.windows-old").Safety);
    }

    [Fact]
    public async Task Hibernation_file_can_be_turned_off_but_page_file_is_kept()
    {
        _layout.File("hiberfil.sys", 2 * MB);
        _layout.File("pagefile.sys", 3 * MB);

        var findings = await Run(new SystemFilesDetector(), _layout.Context());

        var hibernation = Assert.Single(findings, f => f.Id == "win.hibernation");
        Assert.Equal(new RunElevated(HelperOperation.HibernateOff), hibernation.Action);
        var pageFile = Assert.Single(findings, f => f.Id == "win.page-file");
        Assert.Equal(Safety.Keep, pageFile.Safety);
        Assert.Equal(3 * MB, pageFile.Bytes);
    }

    [Fact]
    public async Task Recycle_bin_shows_when_not_empty()
    {
        var context = _layout.Context();

        Assert.Empty(await Run(new RecycleBinDetector(new FakeRecycleBin()), context));
        var finding = Assert.Single(await Run(new RecycleBinDetector(new FakeRecycleBin(5 * MB, 12)), context));
        Assert.Equal(5 * MB, finding.Bytes);
        Assert.IsType<EmptyRecycleBin>(finding.Action);
        Assert.StartsWith("12 things", finding.Reason);
    }
}
