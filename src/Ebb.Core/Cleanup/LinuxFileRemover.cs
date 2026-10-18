using Ebb.Core.Platform;

namespace Ebb.Core.Cleanup;

/// <summary>Linux: to the Trash like a file manager does, or deleted for good.</summary>
public sealed class LinuxFileRemover(FreedesktopTrash trash) : IFileRemover
{
    public void Remove(string path, RemovalMode mode)
    {
        if (mode == RemovalMode.RecycleBin)
            trash.MoveToTrash(path);
        else if (Directory.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            FolderDelete.Run(path);
        else
            File.Delete(path);
    }
}
