using Ebb.Core.Platform;
using Ebb.Core.Vhdx;

namespace Ebb.Core.Elevation;

/// <summary>What Ebb.Helper does once it has admin rights. Only these three things, with checked input.</summary>
public sealed class HelperOperations(ICommandRunner runner, Func<long>? systemDriveFreeSpace = null)
{
    private readonly Func<long> _freeSpace = systemDriveFreeSpace ??
        (() => new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!).AvailableFreeSpace);

    public Task<HelperResult> RunAsync(HelperRequest request, CancellationToken cancellationToken = default) =>
        request.Operation switch
        {
            HelperOperation.HibernateOff => MeasuredAsync("powercfg.exe", ["/hibernate", "off"], TimeSpan.FromMinutes(2),
                "Hibernation is off and the hibernation file is gone.", cancellationToken),
            HelperOperation.ComponentCleanup => MeasuredAsync("DISM.exe", ["/Online", "/Cleanup-Image", "/StartComponentCleanup"],
                TimeSpan.FromMinutes(90), "Windows finished cleaning up old system files.", cancellationToken),
            HelperOperation.CompactVhdx => CompactAsync(request.Path, cancellationToken),
            _ => Task.FromResult(new HelperResult(false, "Unknown operation.")),
        };

    private async Task<HelperResult> MeasuredAsync(string tool, string[] arguments, TimeSpan timeout, string success,
        CancellationToken cancellationToken)
    {
        var before = _freeSpace();
        var result = await runner.RunAsync(tool, arguments, timeout, cancellationToken);
        var freed = _freeSpace() - before;
        return result.Succeeded
            ? new HelperResult(true, success, freed)
            : new HelperResult(false, $"{tool} failed: {FirstLine(result.Error, result.Output)}");
    }

    private async Task<HelperResult> CompactAsync(string? path, CancellationToken cancellationToken)
    {
        if (path is null || !Path.IsPathFullyQualified(path) ||
            !path.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return new HelperResult(false, "Only existing .vhdx files can be shrunk.");

        var before = new FileInfo(path).Length;
        var result = await HasOptimizeVhdAsync(cancellationToken)
            ? await runner.RunAsync("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-Command", $"Optimize-VHD -LiteralPath '{path.Replace("'", "''")}' -Mode Full"],
                TimeSpan.FromHours(2), cancellationToken)
            : await DiskpartAsync(DiskpartScript.Compact(path), cancellationToken);

        if (!result.Succeeded)
        {
            // If compacting failed half way, make sure the disk isn't left attached.
            await DiskpartAsync(DiskpartScript.Detach(path), CancellationToken.None);
            return new HelperResult(false, $"Shrinking failed: {FirstLine(result.Error, result.Output)}");
        }

        var after = new FileInfo(path).Length;
        return new HelperResult(true, "Disk shrunk.", before - after);
    }

    private async Task<bool> HasOptimizeVhdAsync(CancellationToken cancellationToken)
    {
        var check = await runner.RunAsync("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", "Get-Command Optimize-VHD -ErrorAction Stop | Out-Null"],
            TimeSpan.FromSeconds(30), cancellationToken);
        return check.Succeeded;
    }

    private async Task<CommandResult> DiskpartAsync(string script, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"ebb-diskpart-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(scriptPath, script, cancellationToken);
        try
        {
            return await runner.RunAsync("diskpart.exe", ["/s", scriptPath], TimeSpan.FromHours(2), cancellationToken);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    private static string FirstLine(params string[] texts) =>
        texts.SelectMany(t => t.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .FirstOrDefault() ?? "no details";
}
