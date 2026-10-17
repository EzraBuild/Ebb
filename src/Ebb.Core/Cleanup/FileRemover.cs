using System.Runtime.InteropServices;

namespace Ebb.Core.Cleanup;

public enum RemovalMode
{
    RecycleBin,
    Permanent,
}

/// <summary>The user said no when Windows asked to delete something permanently.</summary>
public sealed class RemovalDeclinedException(string message) : Exception(message);

public interface IFileRemover
{
    /// <summary>Removes one file or folder. Throws if it couldn't.</summary>
    void Remove(string path, RemovalMode mode);
}

public sealed class ShellFileRemover : IFileRemover
{
    public void Remove(string path, RemovalMode mode)
    {
        if (mode == RemovalMode.RecycleBin)
            ShellDelete(path, toRecycleBin: true);
        else if (Directory.Exists(path))
            DeleteFolder(path);
        else
            DeleteFile(path);
    }

    /// <summary>Deletes through the Windows shell, like Explorer does.</summary>
    internal static void ShellDelete(string path, bool toRecycleBin)
    {
        var flags = (ushort)(FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT);
        if (toRecycleBin)
            flags |= FOF_ALLOWUNDO | FOF_WANTNUKEWARNING; // ask before deleting anything that doesn't fit in the bin
        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + '\0', // the list must end with two nulls; the marshaller adds one
            fFlags = flags,
        };
        var result = SHFileOperation(ref operation);
        if (operation.fAnyOperationsAborted)
            throw new RemovalDeclinedException("Kept: you chose not to delete it permanently.");
        if (result != 0)
        {
            throw new IOException(result switch
            {
                0x79 or 0x81 => "The path is too long for the Recycle Bin.",
                0x20 => "It's in use by another app.",
                0x5 or 0x78 => "Windows denied access.",
                _ => $"Windows couldn't move it to the Recycle Bin (code 0x{result:X}). It may be in use.",
            });
        }
    }

    private static void DeleteFile(string path)
    {
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    private static void DeleteFolder(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
            // Read-only files (common in .git and node_modules) block Directory.Delete.
            foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, recursive: true);
        }
    }

    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x4;
    private const ushort FOF_NOCONFIRMATION = 0x10;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOERRORUI = 0x400;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public nint hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public nint hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
