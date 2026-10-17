using System.ComponentModel;
using System.Diagnostics;

namespace Ebb.Core.Elevation;

/// <summary>Starts Ebb.Helper.exe with admin rights. Windows shows its UAC prompt every time.</summary>
public sealed class HelperProcessRunner(string helperPath) : IElevatedRunner
{
    private const int ErrorCancelled = 1223;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Ebb.Helper.exe");

    public async Task<HelperResult> RunAsync(HelperRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(helperPath))
            return new HelperResult(false, "Ebb.Helper.exe is missing next to Ebb.exe.");

        var folder = Path.Combine(Path.GetTempPath(), "Ebb");
        Directory.CreateDirectory(folder);
        var resultPath = Path.Combine(folder, $"ebb-helper-{Guid.NewGuid():N}.json");

        var startInfo = new ProcessStartInfo(helperPath) { UseShellExecute = true, Verb = "runas" };
        foreach (var argument in HelperArgs.Build(request, resultPath))
            startInfo.ArgumentList.Add(argument);

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return new HelperResult(false, "Admin permission was declined, so nothing changed.");
        }
        if (process is null)
            return new HelperResult(false, "Couldn't start the admin helper.");

        using (process)
            await process.WaitForExitAsync(cancellationToken);

        if (!File.Exists(resultPath))
            return new HelperResult(false, "The admin helper stopped without reporting back.");
        try
        {
            return HelperArgs.ReadResult(resultPath);
        }
        finally
        {
            File.Delete(resultPath);
        }
    }
}
