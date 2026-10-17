using Ebb.Core.Elevation;
using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

/// <summary>hiberfil.sys, pagefile.sys and swapfile.sys at the root of the system drive.</summary>
public sealed class SystemFilesDetector : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var drive = context.Paths.SystemDrive;

        var hiberfil = Path.Combine(drive, "hiberfil.sys");
        if (context.SizeOf(hiberfil) is var hiberBytes and > 0)
        {
            yield return new Finding
            {
                Id = "win.hibernation",
                Category = Category.Windows,
                Title = "Hibernation file",
                Reason = "Lets your PC hibernate and makes Fast Startup work. Turning hibernation off removes this " +
                         "file; you can turn it back on later with “powercfg /h on”. Asks for admin.",
                Safety = Safety.CheckFirst,
                Bytes = hiberBytes,
                Path = hiberfil,
                Action = new RunElevated(HelperOperation.HibernateOff),
            };
        }

        var pageFiles = new[] { "pagefile.sys", "swapfile.sys" }.Select(name => Path.Combine(drive, name)).ToList();
        var pageBytes = pageFiles.Sum(context.SizeOf);
        if (pageBytes > 0)
        {
            yield return new Finding
            {
                Id = "win.page-file",
                Category = Category.Windows,
                Title = "Page file",
                Reason = "Windows uses this as extra memory when RAM runs out. Don't delete it. You can change its " +
                         "size under Performance settings, Advanced, Virtual memory.",
                Safety = Safety.Keep,
                Bytes = pageBytes,
                Path = pageFiles[0],
                Action = new OpenTarget("SystemPropertiesPerformance.exe", null, "Open performance settings"),
            };
        }
    }
}
