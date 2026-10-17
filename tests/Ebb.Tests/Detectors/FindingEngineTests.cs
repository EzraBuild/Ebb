using Ebb.Core.Findings;
using static Ebb.Tests.FakeLayout;

namespace Ebb.Tests.Detectors;

public class FindingEngineTests : IDisposable
{
    private readonly FakeLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    [Fact]
    public async Task A_failing_detector_does_not_hide_the_others()
    {
        var engine = new FindingEngine([new Broken(), new Fixed(Make("ok", Category.Windows, 2 * MB))]);

        var report = await engine.RunAsync(_layout.Context());

        Assert.Equal("ok", Assert.Single(report.Findings).Id);
        Assert.Contains("Broken", Assert.Single(report.Problems));
    }

    [Fact]
    public async Task Drops_tiny_and_out_of_scope_findings_and_sorts_by_category_then_size()
    {
        var context = _layout.Context();
        var engine = new FindingEngine([new Fixed(
            Make("small", Category.Developer, 10),
            Make("elsewhere", Category.Developer, 9 * MB, path: @"Z:\nowhere"),
            Make("unknown", Category.Windows, null),
            Make("win-big", Category.Windows, 5 * MB),
            Make("dev", Category.Developer, 2 * MB),
            Make("personal", Category.Personal, 3 * MB))]);

        var report = await engine.RunAsync(context);

        Assert.Equal(["dev", "win-big", "unknown", "personal"], report.Findings.Select(f => f.Id));
    }

    private static Finding Make(string id, Category category, long? bytes, string? path = null) => new()
    {
        Id = id,
        Category = category,
        Title = id,
        Reason = "",
        Safety = Safety.Safe,
        Bytes = bytes,
        Path = path,
    };

    private sealed class Broken : IDetector
    {
        public Task<IReadOnlyList<Finding>> DetectAsync(ScanContext context, CancellationToken cancellationToken) =>
            throw new IOException("disk on fire");
    }

    private sealed class Fixed(params Finding[] findings) : IDetector
    {
        public Task<IReadOnlyList<Finding>> DetectAsync(ScanContext context, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Finding>>(findings);
    }
}
