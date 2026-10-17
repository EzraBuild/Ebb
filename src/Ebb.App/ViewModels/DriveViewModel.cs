using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ebb.App.ViewModels;

public sealed partial class DriveViewModel(string root, long totalBytes, long freeBytes) : ObservableObject
{
    public string Root { get; } = root;

    public string Letter => Root.Length <= 3 ? Root.TrimEnd('\\') : Path.GetFileName(Root.TrimEnd('\\'));

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
        if (Root.Length > 3)
            return;
        var drive = new DriveInfo(Root);
        if (!drive.IsReady)
            return;
        TotalBytes = drive.TotalSize;
        FreeBytes = drive.AvailableFreeSpace;
    }

    public static IReadOnlyList<DriveViewModel> FixedDrives() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => new DriveViewModel(d.RootDirectory.FullName, d.TotalSize, d.AvailableFreeSpace))
            .ToList();
}
