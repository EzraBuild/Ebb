namespace Ebb.Core.Elevation;

public sealed record HelperRequest(HelperOperation Operation, string? Path = null);

/// <param name="FreedBytes">How much more free space the drive had afterwards, when measured.</param>
public sealed record HelperResult(bool Succeeded, string Message, long? FreedBytes = null);

/// <summary>Runs one <see cref="HelperOperation"/> with admin rights (Windows shows a UAC prompt).</summary>
public interface IElevatedRunner
{
    Task<HelperResult> RunAsync(HelperRequest request, CancellationToken cancellationToken = default);
}

public static class HelperCommands
{
    /// <summary>The exact Windows command behind each operation, as shown to the user before running.</summary>
    public static string Describe(HelperOperation operation, string? path = null) => operation switch
    {
        HelperOperation.HibernateOff => "powercfg /hibernate off",
        HelperOperation.ComponentCleanup => "DISM /Online /Cleanup-Image /StartComponentCleanup",
        HelperOperation.CompactVhdx => $"diskpart: select vdisk file=\"{path}\" · attach vdisk readonly · compact vdisk · detach vdisk",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
}
