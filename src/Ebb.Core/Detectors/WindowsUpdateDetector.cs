using Ebb.Core.Elevation;
using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

/// <summary>Update and upgrade leftovers. All removed with Windows' own tools, never by hand.</summary>
public sealed class WindowsUpdateDetector : Detector
{
    private const string SystemFilesHint = "In Disk Cleanup, choose “Clean up system files”, then tick ";

    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var p = context.Paths;
        var drive = p.SystemDrive;
        var diskCleanup = WindowsTools.DiskCleanup(p);

        var downloads = Path.Combine(p.Windows, "SoftwareDistribution", "Download");
        if (context.SizeOf(downloads) is var downloadBytes and > 0)
        {
            yield return new Finding
            {
                Id = "win.update-downloads",
                Category = Category.Windows,
                Title = "Windows Update downloads",
                Reason = "Update files Windows has already installed. " + SystemFilesHint + "“Windows Update Cleanup”.",
                Safety = Safety.Safe,
                Bytes = downloadBytes,
                Path = downloads,
                Action = diskCleanup,
            };
        }

        string[] upgradeFolders = [Path.Combine(drive, "$WINDOWS.~BT"), Path.Combine(drive, "$Windows.~WS")];
        foreach (var folder in upgradeFolders)
        {
            if (context.SizeOf(folder) is var bytes and > 0)
            {
                yield return new Finding
                {
                    Id = "win.upgrade-leftovers:" + Path.GetFileName(folder),
                    Category = Category.Windows,
                    Title = "Windows upgrade leftovers",
                    Reason = "Setup files from a Windows upgrade. If no upgrade is in progress, they're not needed. " +
                             SystemFilesHint + "“Temporary Windows installation files”.",
                    Safety = Safety.CheckFirst,
                    Bytes = bytes,
                    Path = folder,
                    Action = diskCleanup,
                };
            }
        }

        var windowsOld = Path.Combine(drive, "Windows.old");
        if (context.SizeOf(windowsOld) is var oldBytes and > 0)
        {
            yield return new Finding
            {
                Id = "win.windows-old",
                Category = Category.Windows,
                Title = "Previous Windows version",
                Reason = "Your old Windows install, kept so you can go back after an upgrade. Removing it means you " +
                         "can't roll back. " + SystemFilesHint + "“Previous Windows installation(s)”.",
                Safety = Safety.CheckFirst,
                Bytes = oldBytes,
                Path = windowsOld,
                Action = diskCleanup,
            };
        }

        var winSxS = Path.Combine(p.Windows, "WinSxS");
        if (context.Covers(winSxS))
        {
            yield return new Finding
            {
                Id = "win.component-store",
                Category = Category.Windows,
                Title = "Old system file versions",
                Reason = "After updates, Windows keeps older copies of system files. Windows' own DISM cleanup " +
                         "removes the ones nothing needs any more. Takes a few minutes and asks for admin.",
                Safety = Safety.Safe,
                Bytes = null,
                Path = winSxS,
                Action = new RunElevated(HelperOperation.ComponentCleanup),
            };
        }
    }
}
