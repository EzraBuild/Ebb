using Ebb.Core.Detectors;
using Ebb.Core.Findings;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.Detectors;

public class PersonalDetectorTests : IDisposable
{
    private readonly FakeLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    [Fact]
    public async Task Splits_old_installers_from_other_downloads()
    {
        _layout.File(@"Users\me\Downloads\setup.exe", 2 * MB, _layout.DaysAgo(90));
        _layout.File(@"Users\me\Downloads\new-setup.msi", MB, _layout.DaysAgo(3));
        _layout.File(@"Users\me\Downloads\report.pdf", MB, _layout.DaysAgo(90));

        var findings = await Run(new DownloadsDetector(), _layout.Context());

        var installers = Assert.Single(findings, f => f.Id == "personal.downloads.installers");
        Assert.Equal("setup.exe", Path.GetFileName(Assert.Single(installers.Items).Path));
        var rest = Assert.Single(findings, f => f.Id == "personal.downloads");
        Assert.Equal(2 * MB, rest.Bytes);
        Assert.IsType<OpenTarget>(rest.Action);
    }

    [Fact]
    public async Task Large_videos_are_listed_but_never_preselected()
    {
        _layout.File(@"Users\me\Videos\holiday.mp4", 3 * MB);
        _layout.File(@"Users\me\Videos\small.mp4", MB / 2);
        _layout.File(@"Users\me\AppData\Local\SomeApp\intro.mp4", 3 * MB);
        _layout.File(@"Users\me\Videos\project.psd", 3 * MB);

        var finding = Assert.Single(await Run(new LargeVideosDetector(minBytes: 2 * MB), _layout.Context()));

        var video = Assert.Single(finding.Items);
        Assert.EndsWith("holiday.mp4", video.Path);
        Assert.False(video.SelectedByDefault);
    }

    [Fact]
    public async Task Duplicates_keep_the_oldest_copy()
    {
        var same = Enumerable.Range(0, 2 * (int)MB).Select(i => (byte)(i % 251)).ToArray();
        var almost = same.ToArray();
        almost[^1] ^= 0xFF; // same size and same start, different end
        _layout.Sandbox.File(@"Users\me\Documents\original.bin", same, _layout.DaysAgo(30));
        _layout.Sandbox.File(@"Users\me\Downloads\copy.bin", same, _layout.DaysAgo(1));
        _layout.Sandbox.File(@"Users\me\Desktop\copy (2).bin", same, _layout.DaysAgo(2));
        _layout.Sandbox.File(@"Users\me\Pictures\different.bin", almost, _layout.DaysAgo(40));
        _layout.Sandbox.File(@"Users\me\Documents\app\node_modules\pkg\same.bin", same, _layout.DaysAgo(9));
        _layout.Sandbox.File(@"Users\me\Documents\app\.git\objects\same.bin", same, _layout.DaysAgo(9));

        var finding = Assert.Single(await Run(new DuplicatesDetector(new DuplicateFinder(MB)), _layout.Context()));

        Assert.Equal(["copy (2).bin", "copy.bin"], finding.Items.Select(i => Path.GetFileName(i.Path)).Order());
        Assert.All(finding.Items, i => Assert.Equal(@"same as ~\Documents\original.bin", i.Note));
        Assert.Equal(4 * MB, finding.Bytes);
    }
}
