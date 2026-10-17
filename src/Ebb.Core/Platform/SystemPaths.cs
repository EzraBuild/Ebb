using System.Runtime.InteropServices;

namespace Ebb.Core.Platform;

/// <summary>Where Windows keeps things. Tests point this at a fake layout in a temp folder.</summary>
public sealed record SystemPaths
{
    public required string SystemDrive { get; init; }
    public required string Windows { get; init; }
    public required string ProgramFiles { get; init; }
    public required string ProgramFilesX86 { get; init; }
    public required string ProgramData { get; init; }
    public required string UserProfile { get; init; }
    public required string LocalAppData { get; init; }
    public required string RoamingAppData { get; init; }
    public required string Temp { get; init; }
    public required string Downloads { get; init; }

    /// <summary>Desktop, Documents, Downloads, Pictures, Videos and Music.</summary>
    public required IReadOnlyList<string> PersonalFolders { get; init; }

    public static SystemPaths FromEnvironment()
    {
        static string Folder(Environment.SpecialFolder folder) =>
            Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);

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
