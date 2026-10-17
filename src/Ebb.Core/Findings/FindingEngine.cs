namespace Ebb.Core.Findings;

public interface IDetector
{
    Task<IReadOnlyList<Finding>> DetectAsync(ScanContext context, CancellationToken cancellationToken);
}

/// <summary>Base for detectors that only look at the file system.</summary>
public abstract class Detector : IDetector
{
    public Task<IReadOnlyList<Finding>> DetectAsync(ScanContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Finding>>(Detect(context, cancellationToken).ToList());

    protected abstract IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken);
}

public sealed record FindingReport(IReadOnlyList<Finding> Findings, IReadOnlyList<string> Problems);

/// <summary>Runs all detectors side by side. One failing detector never hides the others.</summary>
public sealed class FindingEngine
{
    private readonly IReadOnlyList<IDetector> _detectors;
    private readonly long _minBytes;

    public FindingEngine(IEnumerable<IDetector> detectors, long minBytes = 1024 * 1024)
    {
        _detectors = detectors.ToList();
        _minBytes = minBytes;
    }

    public async Task<FindingReport> RunAsync(ScanContext context, CancellationToken cancellationToken = default)
    {
        var runs = _detectors.Select(async detector =>
        {
            try
            {
                return (Findings: await Task.Run(() => detector.DetectAsync(context, cancellationToken), cancellationToken),
                    Problem: (string?)null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (Findings: (IReadOnlyList<Finding>)[], Problem: $"{detector.GetType().Name}: {ex.Message}");
            }
        });
        var results = await Task.WhenAll(runs);

        var findings = results
            .SelectMany(r => r.Findings)
            .Where(f => f.Path is null || context.Covers(f.Path))
            .Where(f => f.Bytes is null || f.Bytes >= _minBytes)
            .OrderBy(f => f.Category)
            .ThenByDescending(f => f.Bytes ?? -1)
            .ToList();
        var problems = results.Select(r => r.Problem).OfType<string>().ToList();
        return new FindingReport(findings, problems);
    }
}
