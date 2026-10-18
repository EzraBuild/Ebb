using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ebb.Core.Platform;

/// <summary>Where the system and your apps keep things. Tests point this at a fake layout in a temp folder.</summary>
public sealed record SystemPaths
{
    /// <summary>Folders right under / that belong to Linux and installed packages.</summary>
    private static readonly string[] LinuxSystemDirs =
    [
        "bin", "boot", "dev", "efi", "etc", "lib", "lib32", "lib64", "lost+found", "nix", "opt", "proc", "root",
        "run", "sbin", "snap", "srv", "sys", "usr", "var",
    ];

    public bool IsLinux { get; init; }

    /// <summary>The drive Windows is on (<c>C:\</c>), or <c>/</c> on Linux.</summary>
    public required string SystemDrive { get; init; }

    /// <summary>Your user folder (your home folder on Linux).</summary>
    public required string UserProfile { get; init; }

    public required string Temp { get; init; }

    public required string Downloads { get; init; }

    /// <summary>Desktop, Documents, Downloads, Pictures, Videos and Music.</summary>
    public required IReadOnlyList<string> PersonalFolders { get; init; }

    // Windows only.
    public string Windows { get; init; } = "";
    public string ProgramFiles { get; init; } = "";
    public string ProgramFilesX86 { get; init; } = "";
    public string ProgramData { get; init; } = "";
    public string LocalAppData { get; init; } = "";
    public string RoamingAppData { get; init; } = "";

    // Linux only: where apps keep things for you (XDG base directories).
    /// <summary><c>~/.cache</c></summary>
    public string Cache { get; init; } = "";
    /// <summary><c>~/.config</c></summary>
    public string Config { get; init; } = "";
    /// <summary><c>~/.local/share</c></summary>
    public string Data { get; init; } = "";

    /// <summary>The system and installed apps. Never touched directly.</summary>
    public IReadOnlyList<string> SystemFolders => IsLinux
        ? LinuxSystemDirs.Select(dir => Path.Combine(SystemDrive, dir)).ToList()
        : [Windows, ProgramFiles, ProgramFilesX86, ProgramData];

    /// <summary>Where apps keep their own files for you: AppData on Windows, ~/.cache, ~/.config and ~/.local on Linux.</summary>
    public IReadOnlyList<string> AppDataFolders => IsLinux
        ? [Cache, Config, Path.GetDirectoryName(Data)!]
        : [LocalAppData, RoamingAppData];

    /// <summary>The Trash on Linux (<c>~/.local/share/Trash</c>).</summary>
    public string Trash => Path.Combine(Data, "Trash");

    public static SystemPaths FromEnvironment() => OperatingSystem.IsWindows() ? WindowsFromEnvironment() : LinuxFromEnvironment();

    /// <summary>A Windows-like layout under <paramref name="root"/>, for tests.</summary>
    public static SystemPaths UnderRoot(string root)
    {
        var profile = Path.Combine(root, "Users", "me");
        var local = Path.Combine(profile, "AppData", "Local");
        var downloads = Path.Combine(profile, "Downloads");
        return new SystemPaths
        {
            SystemDrive = root,
            Windows = Path.Combine(root, "Windows"),
            ProgramFiles = Path.Combine(root, "Program Files"),
            ProgramFilesX86 = Path.Combine(root, "Program Files (x86)"),
            ProgramData = Path.Combine(root, "ProgramData"),
            UserProfile = profile,
            LocalAppData = local,
            RoamingAppData = Path.Combine(profile, "AppData", "Roaming"),
            Temp = Path.Combine(local, "Temp"),
            Downloads = downloads,
            PersonalFolders = new[] { "Desktop", "Documents", "Downloads", "Pictures", "Videos", "Music" }
                .Select(name => Path.Combine(profile, name)).ToList(),
        };
    }

    /// <summary>A Linux-like layout under <paramref name="root"/>, for tests.</summary>
    public static SystemPaths LinuxUnderRoot(string root)
    {
        var home = Path.Combine(root, "home", "me");
        return new SystemPaths
        {
            IsLinux = true,
            SystemDrive = root,
            UserProfile = home,
            Cache = Path.Combine(home, ".cache"),
            Config = Path.Combine(home, ".config"),
            Data = Path.Combine(home, ".local", "share"),
            Temp = Path.Combine(root, "tmp"),
            Downloads = Path.Combine(home, "Downloads"),
            PersonalFolders = new[] { "Desktop", "Documents", "Downloads", "Pictures", "Videos", "Music" }
                .Select(name => Path.Combine(home, name)).ToList(),
        };
    }

    [SupportedOSPlatform("windows")]
    private static SystemPaths WindowsFromEnvironment()
    {
        var windows = Folder(Environment.SpecialFolder.Windows);
        var profile = Folder(Environment.SpecialFolder.UserProfile);
        var downloads = DownloadsFolder() ?? Path.Combine(profile, "Downloads");
        string[] personal =
        [
            Folder(Environment.SpecialFolder.DesktopDirectory),
            Folder(Environment.SpecialFolder.MyDocuments),
            downloads,
            Folder(Environment.SpecialFolder.MyPictures),
            Folder(Environment.SpecialFolder.MyVideos),
            Folder(Environment.SpecialFolder.MyMusic),
        ];

        return new SystemPaths
        {
            SystemDrive = Path.GetPathRoot(windows)!,
            Windows = windows,
            ProgramFiles = Folder(Environment.SpecialFolder.ProgramFiles),
            ProgramFilesX86 = Folder(Environment.SpecialFolder.ProgramFilesX86),
            ProgramData = Folder(Environment.SpecialFolder.CommonApplicationData),
            UserProfile = profile,
            LocalAppData = Folder(Environment.SpecialFolder.LocalApplicationData),
            RoamingAppData = Folder(Environment.SpecialFolder.ApplicationData),
            Temp = PathUtil.Normalize(Path.GetTempPath()),
            Downloads = downloads,
            PersonalFolders = personal.Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    private static SystemPaths LinuxFromEnvironment()
    {
        var home = Folder(Environment.SpecialFolder.UserProfile);
        string Xdg(string variable, string fallback) =>
            Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value && Path.IsPathRooted(value)
                ? PathUtil.Normalize(value)
                : Path.Combine(home, fallback);

        var config = Xdg("XDG_CONFIG_HOME", ".config");
        var userDirs = UserDirs.Read(Path.Combine(config, "user-dirs.dirs"), home);
        string UserDir(string name, string fallback) => userDirs.GetValueOrDefault(name) ?? Path.Combine(home, fallback);

        var downloads = UserDir("DOWNLOAD", "Downloads");
        string[] personal =
        [
            UserDir("DESKTOP", "Desktop"), UserDir("DOCUMENTS", "Documents"), downloads,
            UserDir("PICTURES", "Pictures"), UserDir("VIDEOS", "Videos"), UserDir("MUSIC", "Music"),
        ];

        return new SystemPaths
        {
            IsLinux = true,
            SystemDrive = "/",
            UserProfile = home,
            Cache = Xdg("XDG_CACHE_HOME", ".cache"),
            Config = config,
            Data = Xdg("XDG_DATA_HOME", Path.Combine(".local", "share")),
            Temp = PathUtil.Normalize(Path.GetTempPath()),
            Downloads = downloads,
            // A user dir set to your home folder itself (some setups do this) isn't a personal folder of its own.
            PersonalFolders = personal.Where(p => !PathUtil.SamePath(p, home)).Distinct(PathUtil.Comparer).ToList(),
        };
    }

    private static string Folder(Environment.SpecialFolder folder) =>
        Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);

    [SupportedOSPlatform("windows")]
    private static string? DownloadsFolder()
    {
        var downloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        if (NativeMethods.SHGetKnownFolderPath(downloadsId, 0, 0, out var buffer) != 0)
            return null;
        try
        {
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }
}

/// <summary>Reads <c>~/.config/user-dirs.dirs</c>, where Linux desktops say which folder is Downloads, Music and so on.</summary>
public static class UserDirs
{
    public static Dictionary<string, string> Read(string file, string home)
    {
        var dirs = new Dictionary<string, string>();
        if (!File.Exists(file))
            return dirs;
        foreach (var line in File.ReadLines(file))
            if (Parse(line, home) is var (name, path))
                dirs[name] = path;
        return dirs;
    }

    /// <summary>Parses a line like <c>XDG_DOWNLOAD_DIR="$HOME/Downloads"</c> into ("DOWNLOAD", "/home/me/Downloads").</summary>
    public static (string Name, string Path)? Parse(string line, string home)
    {
        line = line.Trim();
        var equals = line.IndexOf("_DIR=", StringComparison.Ordinal);
        if (!line.StartsWith("XDG_", StringComparison.Ordinal) || equals < 4)
            return null;

        var name = line[4..equals];
        var value = line[(equals + 5)..].Trim().Trim('"');
        if (value.StartsWith("$HOME", StringComparison.Ordinal))
            value = home + value[5..];
        return value.StartsWith('/') && value.Length > 1 ? (name, value.TrimEnd('/')) : null;
    }
}
