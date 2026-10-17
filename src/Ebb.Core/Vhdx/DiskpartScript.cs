namespace Ebb.Core.Vhdx;

/// <summary>diskpart scripts for shrinking a virtual disk. The disk is attached read-only, so its contents can't change.</summary>
public static class DiskpartScript
{
    public static string Compact(string vhdxPath) =>
        $"select vdisk file=\"{vhdxPath}\"\r\nattach vdisk readonly\r\ncompact vdisk\r\ndetach vdisk\r\n";

    public static string Detach(string vhdxPath) =>
        $"select vdisk file=\"{vhdxPath}\"\r\ndetach vdisk\r\n";
}
