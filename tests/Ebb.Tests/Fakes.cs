using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Core.Scanning;

namespace Ebb.Tests;

/// <summary>A fake Windows layout (C:\Windows, Users\me\AppData, ...) inside a sandbox.</summary>
internal sealed class FakeLayout : IDisposable
{
    public const long MB = 1024 * 1024;

    public FakeLayout()
    {
        Paths = SystemPaths.UnderRoot(Sandbox.Root);
    }

    public TestSandbox Sandbox { get; } = new();

    public SystemPaths Paths { get; }

    public DateTime Now { get; } = DateTime.UtcNow;

    public DateTime DaysAgo(int days) => Now.AddDays(-days);

    public string File(string relative, long bytes, DateTime? lastWriteUtc = null, byte fill = 0x2A) =>
        Sandbox.File(relative, bytes, lastWriteUtc, fill);

    public string Dir(string relative, DateTime? lastWriteUtc = null) => Sandbox.Dir(relative, lastWriteUtc);

    public string PathOf(string relative) => Sandbox.PathOf(relative);

    public ScanContext Context(long largeFile = MB)
    {
        var scan = new DriveScanner(new ScanOptions { LargeFileThreshold = largeFile, PruneBelow = 0 }).Scan(Sandbox.Root);
        return new ScanContext(scan, Paths, Now);
    }

    public static async Task<IReadOnlyList<Finding>> Run(IDetector detector, ScanContext context) =>
        await detector.DetectAsync(context, CancellationToken.None);

    public void Dispose() => Sandbox.Dispose();
}

internal sealed class FakeWsl(params WslDistro[] distros) : IWslDistroSource
{
    public IReadOnlyList<WslDistro> GetDistros() => distros;
}

internal sealed class FakeRecycleBin(long bytes = 0, long items = 0) : IRecycleBin
{
    public List<string> Emptied { get; } = [];

    public RecycleBinInfo Query(string drive) => new(bytes, items);

    public void Empty(string drive) => Emptied.Add(drive);
}

internal sealed class FakeTools(params string[] installed) : IToolLocator
{
    public string? Find(string tool) => installed.Contains(tool) ? $@"C:\fake\{tool}.exe" : null;
}

internal sealed class FakeRunner : ICommandRunner
{
    private readonly Func<string, IReadOnlyList<string>, CommandResult> _respond;

    public FakeRunner(Func<string, IReadOnlyList<string>, CommandResult>? respond = null) =>
        _respond = respond ?? ((_, _) => new CommandResult(0, "", ""));

    public List<string> Calls { get; } = [];

    public Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        lock (Calls)
            Calls.Add(string.Join(' ', [Path.GetFileNameWithoutExtension(fileName), .. arguments]));
        return Task.FromResult(_respond(fileName, arguments));
    }
}
