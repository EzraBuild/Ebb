using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

public sealed class VisualStudioDetector : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;

        var vsLocal = Path.Combine(p.LocalAppData, "Microsoft", "VisualStudio");
        var caches = Directory.Exists(vsLocal)
            ? Directory.EnumerateDirectories(vsLocal)
                .Select(instance => Path.Combine(instance, "ComponentModelCache"))
                .Where(Directory.Exists)
                .Select(cache => new FindingItem(cache, context.SizeOf(cache), Directory.GetLastWriteTimeUtc(cache),
                    $"Visual Studio {Path.GetFileName(Path.GetDirectoryName(cache))}"))
                .ToList()
            : [];
        if (caches.Count > 0)
        {
            yield return new Finding
            {
                Id = "dev.vs.component-cache",
                Category = Category.Developer,
                Title = "Visual Studio component caches",
                Reason = "Caches Visual Studio rebuilds by itself the next time it starts. Close Visual Studio first.",
                Safety = Safety.Safe,
                Bytes = caches.Sum(c => c.Bytes),
                Path = vsLocal,
                Items = caches,
                Action = new RemoveItems(),
            };
        }

        var downloadCache = Path.Combine(p.ProgramData, "Microsoft", "VisualStudio", "Packages");
        var installer = Path.Combine(p.ProgramFilesX86, "Microsoft Visual Studio", "Installer", "setup.exe");
        if (context.SizeOf(downloadCache) is var downloadBytes and > 0)
        {
            yield return new Finding
            {
                Id = "dev.vs.download-cache",
                Category = Category.Developer,
                Title = "Visual Studio installer cache",
                Reason = "Setup files Visual Studio keeps after installing. Don't delete them by hand: in Visual Studio " +
                         "Installer, choose Modify, then Installation locations, and untick “Keep download cache " +
                         "after the installation”.",
                Safety = Safety.CheckFirst,
                Bytes = downloadBytes,
                Path = downloadCache,
                Action = File.Exists(installer) ? new OpenTarget(installer, null, "Open Visual Studio Installer") : null,
            };
        }

        var packageCache = Path.Combine(p.ProgramData, "Package Cache");
        if (context.SizeOf(packageCache) is var packageBytes and > 0)
        {
            yield return new Finding
            {
                Id = "win.package-cache",
                Category = Category.Windows,
                Title = "App installer cache",
                Reason = "Installers Windows needs to repair or uninstall apps like Visual Studio and .NET. Deleting " +
                         "them breaks uninstalling, so Ebb leaves them alone.",
                Safety = Safety.Keep,
                Bytes = packageBytes,
                Path = packageCache,
            };
        }
    }
}
