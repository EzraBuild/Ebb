using CommunityToolkit.Mvvm.ComponentModel;
using Ebb.Core.Platform;

namespace Ebb.Linux.ViewModels;

/// <summary>A disk you can scan: / for the main one, or where another disk is mounted.</summary>
public sealed partial class DriveViewModel(string root, long totalBytes, long freeBytes) : ObservableObject
{
    public string Root { get; } = root;

    public bool IsMain => Root == "/";

    /// <summary>"/" for the main disk, otherwise the folder it's mounted on, like "My Stick".</summary>
    public string Label => IsMain ? "/" : Path.GetFileName(Root.TrimEnd('/'));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsedBytes))]
    private long _totalBytes = totalBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsedBytes))]
    private long _freeBytes = freeBytes;

    public long UsedBytes => TotalBytes - FreeBytes;

    /// <summary>Reads the free space again (after a cleanup).</summary>
    public void Refresh()
    {
        try
        {
            var drive = new DriveInfo(Root);
            if (!drive.IsReady)
                return;
            TotalBytes = drive.TotalSize;
            FreeBytes = drive.AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    public static IReadOnlyList<DriveViewModel> Mounted(IReadOnlyList<Mount> mounts)
    {
        var drives = new List<DriveViewModel>();
        foreach (var mount in LinuxMounts.Drives(mounts))
        {
            var drive = new DriveViewModel(mount.Target, 0, 0);
            drive.Refresh();
            if (drive.TotalBytes > 0)
                drives.Add(drive);
        }
        return drives;
    }
}
