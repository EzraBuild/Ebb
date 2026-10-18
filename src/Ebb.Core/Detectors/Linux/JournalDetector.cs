using System.Globalization;
using System.Text.RegularExpressions;
using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>Linux: the systemd journal, the system's logs. Trimmed with journalctl, keeping the newest ones.</summary>
/// <param name="keep">How much of the newest logs to keep. Only shown once the logs are three times that.</param>
public sealed partial class JournalDetector(ICommandRunner runner, IToolLocator tools, long keep = 100L * 1024 * 1024) : IDetector
{
    public async Task<IReadOnlyList<Finding>> DetectAsync(ScanContext context, CancellationToken cancellationToken)
    {
        if (tools.Find("journalctl") is not { } journalctl)
            return [];

        var folder = Path.Combine(context.Paths.SystemDrive, "var", "log", "journal");
        var bytes = context.SizeOf(folder);
        if (bytes == 0)
        {
            // Not everyone may read the system logs; journalctl can still say how big they are.
            var usage = await runner.RunAsync(journalctl, ["--disk-usage"], TimeSpan.FromSeconds(20), cancellationToken);
            bytes = usage.Succeeded ? ParseUsage(usage.Output) ?? 0 : 0;
        }
        if (bytes < 3 * keep)
            return [];

        return
        [
            new Finding
            {
                Id = "linux.journal",
                Category = Category.Windows, // shown as "System" on Linux
                Title = "System logs",
                Reason = $"The systemd journal: logs from the system and your apps. This keeps the newest " +
                         $"{ByteSize.Format(keep)}, so recent logs stay for troubleshooting.",
                Safety = Safety.Safe,
                Bytes = bytes - keep,
                Path = folder,
                Action = new RunTool("journalctl", [$"--vacuum-size={keep / 1024 / 1024}M"], AsRoot: true),
            },
        ];
    }

    /// <summary>Parses "Archived and active journals take up 1.2G in the file system." (binary units).</summary>
    internal static long? ParseUsage(string output)
    {
        var match = UsagePattern().Match(output);
        if (!match.Success)
            return null;
        var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var power = " KMGTP".IndexOf(match.Groups[2].Value is { Length: > 0 } unit ? unit[0] : ' ');
        return (long)Math.Round(value * Math.Pow(1024, Math.Max(power, 0)));
    }

    [GeneratedRegex(@"take up ([\d.]+)\s*([KMGTP]?)")]
    private static partial Regex UsagePattern();
}
