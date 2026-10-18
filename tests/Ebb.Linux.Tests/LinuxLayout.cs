using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Core.Scanning;
using Ebb.Tests;

namespace Ebb.Linux.Tests;

/// <summary>A fake Linux layout (/home/me/.cache, /var/cache, ...) inside a sandbox. Works on Windows too.</summary>
internal sealed class LinuxLayout : IDisposable
{
    public const long MB = 1024 * 1024;

    public LinuxLayout()
    {
        Paths = SystemPaths.LinuxUnderRoot(Sandbox.Root);
    }

    public TestSandbox Sandbox { get; } = new();

    public SystemPaths Paths { get; }

    public DateTime Now { get; } = DateTime.UtcNow;

    public DateTime DaysAgo(int days) => Now.AddDays(-days);

    /// <summary>A file under the fake root; use forward slashes, like "home/me/.cache/pip/x".</summary>
    public string File(string relative, long bytes, DateTime? lastWriteUtc = null) =>
        Sandbox.File(Native(relative), bytes, lastWriteUtc);

    public string Dir(string relative, DateTime? lastWriteUtc = null) => Sandbox.Dir(Native(relative), lastWriteUtc);

    public string PathOf(string relative) => Sandbox.PathOf(Native(relative));

    public ScanContext Context()
    {
        var scan = new DriveScanner(new ScanOptions { LargeFileThreshold = MB, PruneBelow = 0 }).Scan(Sandbox.Root);
        return new ScanContext(scan, Paths, Now);
    }

    public static async Task<IReadOnlyList<Finding>> Run(IDetector detector, ScanContext context) =>
        await detector.DetectAsync(context, CancellationToken.None);

    public void Dispose() => Sandbox.Dispose();

    private static string Native(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);
}
