using System.ComponentModel;
using System.Diagnostics;

namespace Ebb.Core.Platform;

public sealed record CommandResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;

    public static CommandResult NotStarted(string message) => new(-1, "", message);
}

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public interface IToolLocator
{
    /// <summary>Full path of a command-line tool on PATH, or null if it isn't installed.</summary>
    string? Find(string tool);
}

public sealed class PathToolLocator : IToolLocator
{
    public string? Find(string tool)
    {
        if (Path.IsPathRooted(tool))
            return File.Exists(tool) ? tool : null;

        // Linux tools have no extension: "" checks the bare name.
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var folder in folders)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(folder, tool + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}

public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["WSL_UTF8"] = "1"; // wsl.exe prints UTF-16 otherwise

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            return CommandResult.NotStarted(ex.Message);
        }

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new CommandResult(-1, await output, $"Timed out after {timeout.TotalMinutes:0} min");
        }

        return new CommandResult(process.ExitCode, await output, await error);
    }
}
