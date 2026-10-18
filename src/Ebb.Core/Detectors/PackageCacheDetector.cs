using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>Package manager caches. Cleaned with each tool's own command.</summary>
public sealed class PackageCacheDetector : Detector
{
    private sealed record Cache(string Id, string Title, string Reason, string[] Folders, FindingAction Command);

    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        foreach (var cache in Caches(context.Paths))
        {
            var folders = cache.Folders.Where(Directory.Exists).ToList();
            var bytes = folders.Sum(context.SizeOf);
            if (bytes == 0)
                continue;

            yield return new Finding
            {
                Id = cache.Id,
                Category = Category.Developer,
                Title = cache.Title,
                Reason = cache.Reason,
                Safety = Safety.Safe,
                Bytes = bytes,
                Path = folders[0],
                // For RunTool, only used if the tool itself isn't installed.
                Items = folders.SelectMany(f => context.ItemsIn(f)).ToList(),
                Action = cache.Command,
            };
        }
    }

    private static IEnumerable<Cache> Caches(SystemPaths p) => p.IsLinux ? LinuxCaches(p) : WindowsCaches(p);

    private static IEnumerable<Cache> WindowsCaches(SystemPaths p) =>
    [
        new("dev.cache.npm", "npm cache",
            "Copies of every npm package you've downloaded, kept to make installs faster. npm downloads them again when needed.",
            [Path.Combine(p.LocalAppData, "npm-cache")],
            new RunTool("npm", ["cache", "clean", "--force"])),
        new("dev.cache.nuget", "NuGet packages and caches",
            ".NET packages your projects restored. The next build restores what it needs again (needs internet).",
            [
                Path.Combine(p.UserProfile, ".nuget", "packages"),
                Path.Combine(p.LocalAppData, "NuGet", "v3-cache"),
                Path.Combine(p.LocalAppData, "NuGet", "plugins-cache"),
                Path.Combine(p.Temp, "NuGetScratch"),
            ],
            new RunTool("dotnet", ["nuget", "locals", "all", "--clear"])),
        new("dev.cache.pip", "pip cache",
            "Python packages pip downloaded before. pip fetches them again when needed.",
            [Path.Combine(p.LocalAppData, "pip", "Cache")],
            new RunTool("pip", ["cache", "purge"])),
        new("dev.cache.yarn", "Yarn cache",
            "Yarn's download cache. Yarn fetches packages again when needed.",
            [Path.Combine(p.LocalAppData, "Yarn", "Cache")],
            new RunTool("yarn", ["cache", "clean"])),
        new("dev.cache.pnpm", "pnpm store",
            "Packages shared by your pnpm projects. pnpm's prune only removes the ones no project uses any more, " +
            "so it frees part of this.",
            [Path.Combine(p.LocalAppData, "pnpm", "store")],
            new RunTool("pnpm", ["store", "prune"])),
    ];

    private static IEnumerable<Cache> LinuxCaches(SystemPaths p)
    {
        var home = p.UserProfile;
        return
        [
            new("dev.cache.npm", "npm cache",
                "Copies of every npm package you've downloaded, kept to make installs faster. npm downloads them again when needed.",
                [Path.Combine(home, ".npm", "_cacache")],
                new RunTool("npm", ["cache", "clean", "--force"])),
            new("dev.cache.nuget", "NuGet packages and caches",
                ".NET packages your projects restored. The next build restores what it needs again (needs internet).",
                [
                    Path.Combine(home, ".nuget", "packages"),
                    Path.Combine(p.Data, "NuGet", "http-cache"),
                    Path.Combine(p.Data, "NuGet", "plugins-cache"),
                    Path.Combine(p.Temp, "NuGetScratch"),
                ],
                new RunTool("dotnet", ["nuget", "locals", "all", "--clear"])),
            new("dev.cache.pip", "pip cache",
                "Python packages pip downloaded before. pip fetches them again when needed.",
                [Path.Combine(p.Cache, "pip")],
                new RunTool("pip", ["cache", "purge"])),
            new("dev.cache.yarn", "Yarn cache",
                "Yarn's download cache. Yarn fetches packages again when needed.",
                [Path.Combine(p.Cache, "yarn")],
                new RunTool("yarn", ["cache", "clean"])),
            new("dev.cache.pnpm", "pnpm store",
                "Packages shared by your pnpm projects. pnpm's prune only removes the ones no project uses any more, " +
                "so it frees part of this.",
                [Path.Combine(p.Data, "pnpm", "store")],
                new RunTool("pnpm", ["store", "prune"])),
            new("dev.cache.go-build", "Go build cache",
                "Compiled packages Go keeps to make builds faster. The next build makes them again.",
                [Path.Combine(p.Cache, "go-build")],
                new RunTool("go", ["clean", "-cache"])),
            new("dev.cache.go-mod", "Go module cache",
                "Source code of the Go modules your projects downloaded. Go fetches them again when needed (needs internet).",
                [Path.Combine(home, "go", "pkg", "mod")],
                new RunTool("go", ["clean", "-modcache"])),
            new("dev.cache.cargo", "Cargo registry cache",
                "Rust crates Cargo downloaded and unpacked. Cargo fetches them again on the next build (needs internet).",
                [Path.Combine(home, ".cargo", "registry", "cache"), Path.Combine(home, ".cargo", "registry", "src")],
                new RemoveItems(RebuiltBy: "Building a Rust project")),
            new("dev.cache.bun", "Bun cache",
                "Packages Bun downloaded before. Bun fetches them again when needed.",
                [Path.Combine(home, ".bun", "install", "cache")],
                new RunTool("bun", ["pm", "cache", "rm"])),
            new("dev.cache.gradle", "Gradle caches",
                "Dependencies and build outputs Gradle keeps between builds. The next build downloads and makes them again.",
                [Path.Combine(home, ".gradle", "caches")],
                new RemoveItems(RebuiltBy: "The next Gradle build")),
        ];
    }
}
