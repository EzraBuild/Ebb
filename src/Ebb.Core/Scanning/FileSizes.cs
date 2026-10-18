using System.IO.Enumeration;
using System.Runtime.InteropServices;
using Ebb.Core.Platform;

namespace Ebb.Core.Scanning;

/// <summary>How much space a file really takes on this drive.</summary>
public static class FileSizes
{
    // Online-only cloud files (OneDrive and friends) report their full size but take no local space.
    private const FileAttributes CloudOnly =
        FileAttributes.Offline | (FileAttributes)0x00040000 /* RECALL_ON_OPEN */ | (FileAttributes)0x00400000 /* RECALL_ON_DATA_ACCESS */;

    // Sparse files (like WSL disks) and NTFS-compressed files take less than their length.
    private const FileAttributes Shrunk = FileAttributes.SparseFile | FileAttributes.Compressed;

    public static bool IsCloudOnly(FileAttributes attributes) => (attributes & CloudOnly) != 0;

    // A symlink on Linux takes no real space; its target is counted where it lives.
    private static bool IsUnixLink(FileAttributes attributes) =>
        !OperatingSystem.IsWindows() && (attributes & FileAttributes.ReparsePoint) != 0;

    internal static long OnDisk(ref FileSystemEntry entry)
    {
        var attributes = entry.Attributes;
        if (IsCloudOnly(attributes) || IsUnixLink(attributes))
            return 0;
        return (attributes & Shrunk) != 0 ? Allocated(entry.ToFullPath(), entry.Length) : entry.Length;
    }

    public static long OnDisk(FileInfo file)
    {
        if (!file.Exists)
            return 0;
        var attributes = file.Attributes;
        if (IsCloudOnly(attributes) || IsUnixLink(attributes))
            return 0;
        return (attributes & Shrunk) != 0 ? Allocated(file.FullName, file.Length) : file.Length;
    }

    private static long Allocated(string path, long fallback)
    {
        var low = NativeMethods.GetCompressedFileSize(path, out var high);
        if (low == uint.MaxValue && Marshal.GetLastPInvokeError() != 0)
            return fallback;
        return ((long)high << 32) | low;
    }
}
