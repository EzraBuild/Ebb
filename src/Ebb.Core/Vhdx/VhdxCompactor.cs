using System.Diagnostics;
using Ebb.Core.Elevation;
using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Vhdx;

public interface IProcessInspector
{
    bool IsRunning(string processName);
}

public sealed class ProcessInspector : IProcessInspector
{
    public bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var process in processes)
            process.Dispose();
        return processes.Length > 0;
    }
}

/// <summary>
/// Shrinks a WSL or Docker disk: trims free space inside Linux, shuts WSL down so the file is
/// closed, then compacts it with admin rights. Nothing inside the disk is deleted.
/// </summary>
public sealed class VhdxCompactor(ICommandRunner runner, IElevatedRunner elevated, IProcessInspector processes) : IDiskCompactor
{
    public async Task<CompactResult> CompactAsync(CompactDisk disk, CancellationToken cancellationToken = default)
    {
        var before = SizeOf(disk.VhdxPath);
        if (before == 0)
            return new CompactResult(false, "The disk file isn't there any more.", 0, 0);

        if (disk.Owner == DiskOwner.Docker && processes.IsRunning("Docker Desktop"))
            return new CompactResult(false, "Quit Docker Desktop first (right-click its tray icon, then Quit) and try again.", before, before);

        if (disk.Owner == DiskOwner.Wsl && disk.DistroName is { } distro)
        {
            // Tells the virtual disk which blocks are free so compacting can give them back. Optional.
            await runner.RunAsync("wsl.exe", ["-d", distro, "-u", "root", "-e", "fstrim", "-a"], TimeSpan.FromMinutes(5), cancellationToken);
        }

        var shutdown = await runner.RunAsync("wsl.exe", ["--shutdown"], TimeSpan.FromMinutes(2), cancellationToken);
        if (!shutdown.Succeeded)
            return new CompactResult(false, $"Couldn't stop WSL: {shutdown.Error.Trim()}", before, before);

        var result = await elevated.RunAsync(new HelperRequest(HelperOperation.CompactVhdx, disk.VhdxPath), cancellationToken);
        var after = SizeOf(disk.VhdxPath);
        var message = result.Succeeded
            ? $"Shrunk from {ByteSize.Format(before)} to {ByteSize.Format(after)}."
            : result.Message;
        return new CompactResult(result.Succeeded, message, before, after);
    }

    private static long SizeOf(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
}
