using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>Docker Desktop's data disk, plus what <c>docker system prune</c> could free.</summary>
public sealed partial class DockerDetector(IWslDistroSource distros, ICommandRunner runner, IToolLocator tools) : IDetector
{
    private static readonly RunTool Prune = new("docker", ["system", "prune", "-f"]);

    public static bool IsDockerDistro(string name) => name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<Finding>> DetectAsync(ScanContext context, CancellationToken cancellationToken)
    {
        var findings = new List<Finding>();
        var dockerFolder = Path.Combine(context.Paths.LocalAppData, "Docker");

        foreach (var disk in DataDisks(context))
        {
            var bytes = context.SizeOf(disk);
            if (bytes == 0)
                continue;
            findings.Add(new Finding
            {
                Id = "dev.docker.disk:" + disk,
                Category = Category.Developer,
                Title = "Docker disk",
                Reason = "Docker keeps all its images, containers and volumes in this one file. Like WSL disks, it " +
                         "never shrinks by itself. Run the Docker cleanup first, then shrink it to get the space back.",
                Safety = Safety.Keep,
                Bytes = bytes,
                Path = disk,
                Action = new CompactDisk(disk, DiskOwner.Docker, null),
            });
        }

        if (tools.Find("docker") is { } docker)
            findings.Add(await PruneFindingAsync(docker, dockerFolder, cancellationToken));

        return findings;
    }

    private IEnumerable<string> DataDisks(ScanContext context)
    {
        var wsl = Path.Combine(context.Paths.LocalAppData, "Docker", "wsl");
        string[] known = [Path.Combine(wsl, "disk", "docker_data.vhdx"), Path.Combine(wsl, "data", "ext4.vhdx")];
        var registered = distros.GetDistros()
            .Where(d => d.Name.Equals("docker-desktop-data", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.VhdxPath);
        return known.Concat(registered).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Finding> PruneFindingAsync(string docker, string dockerFolder, CancellationToken cancellationToken)
    {
        var df = await runner.RunAsync(docker, ["system", "df", "--format", "{{json .}}"], TimeSpan.FromSeconds(20), cancellationToken);
        var bytes = df.Succeeded ? Reclaimable(df.Output) : null;
        var reason = "Stopped containers, unused networks, dangling images and build cache. Images in use and your " +
                     "volumes are kept. Uses Docker's own cleanup command.";
        if (bytes is null)
            reason += " Docker isn't running right now, so the size is unknown. Start Docker Desktop to run it.";

        return new Finding
        {
            Id = "dev.docker.prune",
            Category = Category.Developer,
            Title = "Docker leftovers",
            Reason = reason,
            Safety = Safety.CheckFirst,
            Bytes = bytes,
            Path = dockerFolder,
            Action = Prune,
        };
    }

    /// <summary>
    /// What <c>docker system prune</c> frees: stopped containers and build cache. Image numbers from
    /// <c>docker system df</c> include unused tagged images, which prune keeps, so they're left out.
    /// </summary>
    internal static long? Reclaimable(string dfJsonLines)
    {
        long total = 0;
        var any = false;
        foreach (var line in dfJsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var json = JsonDocument.Parse(line);
                var type = json.RootElement.GetProperty("Type").GetString();
                any = true;
                if (type is "Containers" or "Build Cache" &&
                    ParseSize(json.RootElement.GetProperty("Reclaimable").GetString()) is { } bytes)
                    total += bytes;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
            }
        }
        return any ? total : null;
    }

    /// <summary>Parses Docker's sizes like "1.37GB (45%)", "512.3MB", "0B" (decimal units).</summary>
    internal static long? ParseSize(string? text)
    {
        var match = SizePattern().Match(text ?? "");
        if (!match.Success)
            return null;
        var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var power = "BKMGTP".IndexOf(char.ToUpperInvariant(match.Groups[2].Value[0]));
        return (long)Math.Round(value * Math.Pow(1000, power));
    }

    [GeneratedRegex(@"^\s*([\d.]+)\s*([kKMGTP]?B)")]
    private static partial Regex SizePattern();
}
