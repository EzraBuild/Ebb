using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>Linux: Flatpak runtimes no app uses any more. Flatpak removes them itself.</summary>
public sealed class FlatpakDetector(IToolLocator tools) : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        if (tools.Find("flatpak") is null)
            yield break;

        var folders = new[] { Path.Combine(context.Paths.Data, "flatpak"), Path.Combine(context.Paths.SystemDrive, "var", "lib", "flatpak") }
            .Where(Directory.Exists)
            .ToList();
        if (folders.Count == 0)
            yield break;

        yield return new Finding
        {
            Id = "linux.flatpak.unused",
            Category = Category.Windows, // shown as "System" on Linux
            Title = "Unused Flatpak runtimes",
            Reason = "Runtimes and extensions left behind after apps updated or were removed. No app uses them. " +
                     "Flatpak removes them itself, and the size isn't known until it runs.",
            Safety = Safety.Safe,
            Bytes = null,
            Path = folders[0],
            Action = new RunTool("flatpak", ["uninstall", "--unused", "-y"]),
        };
    }
}
