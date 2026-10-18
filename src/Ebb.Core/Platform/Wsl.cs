using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Ebb.Core.Platform;

public sealed record WslDistro(string Name, string VhdxPath);

public interface IWslDistroSource
{
    IReadOnlyList<WslDistro> GetDistros();
}

/// <summary>Reads installed WSL distros from the registry (read-only).</summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryWslDistroSource : IWslDistroSource
{
    public IReadOnlyList<WslDistro> GetDistros()
    {
        using var lxss = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
        if (lxss is null)
            return [];

        var distros = new List<WslDistro>();
        foreach (var id in lxss.GetSubKeyNames())
        {
            using var key = lxss.OpenSubKey(id);
            if (key?.GetValue("DistributionName") is not string name || key.GetValue("BasePath") is not string basePath)
                continue;

            var vhd = key.GetValue("VhdFileName") as string ?? "ext4.vhdx";
            if (basePath.StartsWith(@"\\?\", StringComparison.Ordinal))
                basePath = basePath[4..];
            distros.Add(new WslDistro(name, Path.Combine(basePath, vhd)));
        }
        return distros;
    }
}

/// <summary>For Linux, where there is no WSL.</summary>
public sealed class NoWslDistros : IWslDistroSource
{
    public IReadOnlyList<WslDistro> GetDistros() => [];
}
