using Ebb.Core.Findings;
using Ebb.Core.Platform;

namespace Ebb.Core.Detectors;

/// <summary>WSL distros keep everything in one ext4.vhdx that only ever grows.</summary>
public sealed class WslDiskDetector(IWslDistroSource distros) : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        foreach (var distro in distros.GetDistros())
        {
            if (DockerDetector.IsDockerDistro(distro.Name))
                continue; // reported by the Docker detector

            var bytes = context.SizeOf(distro.VhdxPath);
            if (bytes == 0)
                continue;

            yield return new Finding
            {
                Id = $"dev.wsl.{distro.Name}",
                Category = Category.Developer,
                Title = $"WSL disk: {distro.Name}",
                Reason = $"Everything inside your {distro.Name} Linux install lives in this one file. It grows as you " +
                         "add files but never shrinks by itself, even after you delete things inside Linux. " +
                         "Shrinking gives back the empty space and deletes nothing.",
                Safety = Safety.Keep,
                Bytes = bytes,
                Path = distro.VhdxPath,
                Action = new CompactDisk(distro.VhdxPath, DiskOwner.Wsl, distro.Name),
            };
        }
    }
}
