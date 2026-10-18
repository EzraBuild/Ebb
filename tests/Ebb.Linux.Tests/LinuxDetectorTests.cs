using Ebb.Core.Detectors;
using Ebb.Core.Findings;
using Ebb.Core.Platform;
using Ebb.Tests;
using static Ebb.Linux.Tests.LinuxLayout;

namespace Ebb.Linux.Tests;

public class LinuxDetectorTests : IDisposable
{
    private readonly LinuxLayout _layout = new();

    public void Dispose() => _layout.Dispose();

    [Fact]
    public async Task Pacman_cache_is_cleaned_with_paccache_when_it_is_there()
    {
        _layout.File("var/cache/pacman/pkg/linux-6.9-1-x86_64.pkg.tar.zst", 3 * MB);
        var context = _layout.Context();

        var withPaccache = Assert.Single(await Run(new SystemPackageCacheDetector(new FakeTools("pacman", "paccache")), context));
        Assert.Equal("linux.cache.pacman", withPaccache.Id);
        Assert.Equal(3 * MB, withPaccache.Bytes);
        Assert.Equal(new RunTool("paccache", ["-rk1"], AsRoot: true).CommandLine, ((RunTool)withPaccache.Action!).CommandLine);

        var plain = Assert.Single(await Run(new SystemPackageCacheDetector(new FakeTools("pacman")), context));
        Assert.Equal("pkexec pacman -Sc --noconfirm", ((RunTool)plain.Action!).CommandLine);

        Assert.Empty(await Run(new SystemPackageCacheDetector(new FakeTools()), context));
    }

    [Fact]
    public async Task Apt_and_dnf_caches_use_their_own_clean_commands()
    {
        _layout.File("var/cache/apt/archives/vim_9.1_amd64.deb", 2 * MB);
        _layout.File("var/cache/libdnf5/fedora/packages/vim.rpm", 2 * MB);

        var findings = await Run(new SystemPackageCacheDetector(new FakeTools("apt-get", "dnf")), _layout.Context());

        Assert.Equal(["pkexec apt-get clean", "pkexec dnf clean packages"], findings.Select(f => ((RunTool)f.Action!).CommandLine));
        Assert.EndsWith("libdnf5", findings[1].Path);
    }

    [Fact]
    public async Task Developer_caches_are_found_where_linux_keeps_them()
    {
        _layout.File("home/me/.npm/_cacache/content-v2/a", 2 * MB);
        _layout.File("home/me/.cache/pip/http/b", 2 * MB);
        _layout.File("home/me/.local/share/pnpm/store/v3/c", 2 * MB);
        _layout.File("home/me/.cargo/registry/cache/index/crate.crate", 2 * MB);

        var findings = await Run(new PackageCacheDetector(), _layout.Context());

        Assert.Equal(["dev.cache.npm", "dev.cache.pip", "dev.cache.pnpm", "dev.cache.cargo"], findings.Select(f => f.Id));
        Assert.Equal("npm cache clean --force", ((RunTool)findings[0].Action!).CommandLine);
        Assert.Equal("Building a Rust project", ((RemoveItems)findings[3].Action!).RebuiltBy);
    }

    [Fact]
    public async Task App_caches_leave_ones_used_today_unticked_and_skip_covered_ones()
    {
        _layout.File("home/me/.cache/mozilla/firefox/cache2/x", 3 * MB);
        _layout.File("home/me/.cache/mesa_shader_cache/y", 2 * MB);
        _layout.File("home/me/.cache/pip/http/z", 2 * MB);
        _layout.File("home/me/.cache/tiny/t", 100);
        _layout.File("home/me/.cache/yay/google-chrome/google-chrome.pkg.tar.zst", 4 * MB);
        Directory.SetLastWriteTimeUtc(_layout.PathOf("home/me/.cache/mesa_shader_cache"), _layout.DaysAgo(10));
        Directory.SetLastWriteTimeUtc(_layout.PathOf("home/me/.cache/mozilla"), _layout.Now);

        var findings = await Run(new UserCacheDetector(), _layout.Context());

        var aur = findings.Single(f => f.Id == "linux.cache.aur");
        Assert.EndsWith("google-chrome", Assert.Single(aur.Items).Path);
        Assert.Equal("Updating an AUR package", ((RemoveItems)aur.Action!).RebuiltBy);

        var apps = findings.Single(f => f.Id == "linux.cache.apps");
        Assert.Equal(["mozilla", "mesa_shader_cache"], apps.Items.Select(i => Path.GetFileName(i.Path)));
        Assert.False(apps.Items[0].SelectedByDefault); // used today: the browser may be open
        Assert.True(apps.Items[1].SelectedByDefault);
        Assert.NotNull(((RemoveItems)apps.Action!).RebuiltBy); // deleted for good: a cache in the Trash frees nothing
    }

    [Fact]
    public async Task Journal_keeps_the_newest_logs()
    {
        _layout.File("var/log/journal/abc/system.journal", 4 * MB);
        var detector = new JournalDetector(new FakeRunner(), new FakeTools("journalctl"), keep: MB);

        var finding = Assert.Single(await Run(detector, _layout.Context()));

        Assert.Equal(3 * MB, finding.Bytes);
        Assert.Equal("pkexec journalctl --vacuum-size=1M", ((RunTool)finding.Action!).CommandLine);
        Assert.Empty(await Run(new JournalDetector(new FakeRunner(), new FakeTools("journalctl"), keep: 2 * MB), _layout.Context()));
    }

    [Fact]
    public async Task Journal_asks_journalctl_when_the_logs_cannot_be_read()
    {
        var runner = new FakeRunner((_, _) => new CommandResult(0, "Archived and active journals take up 1.0G in the file system.", ""));

        var finding = Assert.Single(await Run(new JournalDetector(runner, new FakeTools("journalctl")), _layout.Context()));

        Assert.Equal(1024 * MB - 100 * MB, finding.Bytes);
        Assert.Contains(runner.Calls, c => c.EndsWith("--disk-usage"));
    }

    [Fact]
    public async Task Trash_and_flatpak_show_up_when_there_is_something()
    {
        _layout.Dir("home/me/.local/share/flatpak/runtime");
        var context = _layout.Context();

        var trash = Assert.Single(await Run(new TrashDetector(new FakeRecycleBin(5 * MB, 12)), context));
        Assert.Equal(new EmptyRecycleBin(_layout.Paths.Trash, IsTrash: true), trash.Action);
        Assert.StartsWith("12 things", trash.Reason);
        Assert.Empty(await Run(new TrashDetector(new FakeRecycleBin()), context));

        var flatpak = Assert.Single(await Run(new FlatpakDetector(new FakeTools("flatpak")), context));
        Assert.Null(flatpak.Bytes);
        Assert.Equal("flatpak uninstall --unused -y", ((RunTool)flatpak.Action!).CommandLine);
    }

    [Fact]
    public async Task Node_modules_in_tool_and_system_folders_are_not_projects()
    {
        _layout.File("home/me/code/shop/package.json", 1);
        _layout.File("home/me/code/shop/node_modules/react/index.js", 2 * MB);
        _layout.File("usr/lib/node_modules/npm/package.json", 1);
        _layout.File("usr/lib/node_modules/npm/node_modules/x/index.js", 2 * MB);
        _layout.File("home/me/.local/share/pnpm/global/5/package.json", 1);
        _layout.File("home/me/.local/share/pnpm/global/5/node_modules/x/index.js", 2 * MB);

        var finding = Assert.Single(await Run(new NodeModulesDetector(), _layout.Context()));

        Assert.EndsWith(Path.Combine("shop", "node_modules"), Assert.Single(finding.Items).Path);
    }

    [Fact]
    public async Task Linux_installers_count_as_old_downloads()
    {
        _layout.File("home/me/Downloads/Obsidian-1.6.AppImage", 2 * MB, _layout.DaysAgo(60));
        _layout.File("home/me/Downloads/discord-0.0.70.deb", 2 * MB, _layout.DaysAgo(60));
        _layout.File("home/me/Downloads/notes.md", 2 * MB, _layout.DaysAgo(60));

        var findings = await Run(new DownloadsDetector(), _layout.Context());

        var installers = findings.Single(f => f.Id == "personal.downloads.installers");
        Assert.Equal(2, installers.Items.Count);
    }

    [Fact]
    public void Linux_list_has_no_windows_detectors()
    {
        var detectors = DefaultDetectors.CreateLinux(new FakeRunner(), new FakeTools(), new FakeRecycleBin());

        Assert.DoesNotContain(detectors, d => d is WslDiskDetector or WindowsUpdateDetector or SystemFilesDetector or RecycleBinDetector);
        Assert.Contains(detectors, d => d is TrashDetector);
    }
}
