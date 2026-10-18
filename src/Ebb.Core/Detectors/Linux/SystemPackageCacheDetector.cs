using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>Linux: downloaded packages your package manager keeps (pacman, apt, dnf). Cleaned with its own command, as root.</summary>
public sealed class SystemPackageCacheDetector(IToolLocator tools) : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var root = context.Paths.SystemDrive;
        string Var(params string[] parts) => Path.Combine([root, "var", .. parts]);

        if (tools.Find("pacman") is not null)
        {
            // paccache (from pacman-contrib) frees more than pacman itself, and keeps a copy to reinstall from.
            var paccache = tools.Find("paccache") is not null;
            yield return Cache(context, "linux.cache.pacman", "pacman package cache", Var("cache", "pacman", "pkg"),
                paccache
                    ? "Every version of every package pacman has downloaded. This keeps the newest copy of each one, so " +
                      "you can still reinstall what you have, and removes the older ones."
                    : "Packages pacman has downloaded. This removes the ones you no longer have installed. Install " +
                      "pacman-contrib to get paccache, which frees more.",
                paccache ? new RunTool("paccache", ["-rk1"], AsRoot: true) : new RunTool("pacman", ["-Sc", "--noconfirm"], AsRoot: true));
        }

        if (tools.Find("apt-get") is not null)
        {
            yield return Cache(context, "linux.cache.apt", "apt package cache", Var("cache", "apt", "archives"),
                "Packages apt downloaded to install or update them. They're no longer needed once installed.",
                new RunTool("apt-get", ["clean"], AsRoot: true));
        }

        if (tools.Find("dnf") is not null)
        {
            var folder = new[] { Var("cache", "libdnf5"), Var("cache", "dnf") }.FirstOrDefault(Directory.Exists) ?? Var("cache", "dnf");
            yield return Cache(context, "linux.cache.dnf", "dnf package cache", folder,
                "Packages dnf downloaded to install or update them. They're no longer needed once installed.",
                new RunTool("dnf", ["clean", "packages"], AsRoot: true));
        }
    }

    private static Finding Cache(ScanContext context, string id, string title, string folder, string reason, RunTool command) =>
        new()
        {
            Id = id,
            Category = Category.Windows, // shown as "System" on Linux
            Title = title,
            Reason = reason,
            Safety = Safety.Safe,
            Bytes = context.SizeOf(folder),
            Path = folder,
            Action = command,
        };
}
