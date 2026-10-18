using System.Globalization;
using Ebb.Core.Cleanup;
using Ebb.Core.Scanning;

namespace Ebb.Core.Platform;

/// <summary>
/// The Linux Trash in your home folder (<c>~/.local/share/Trash</c>), the way file managers use it: each item
/// moves to <c>files/</c> and gets a <c>.trashinfo</c> note in <c>info/</c>, so it can be restored to where it was.
/// </summary>
public sealed class FreedesktopTrash(string trashFolder) : IRecycleBin
{
    private string Files => Path.Combine(trashFolder, "files");

    private string Info => Path.Combine(trashFolder, "info");

    public RecycleBinInfo Query(string drive)
    {
        if (!Directory.Exists(Files))
            return new RecycleBinInfo(0, 0);
        var items = Directory.EnumerateFileSystemEntries(Files).Count();
        return new RecycleBinInfo(DirectorySizer.Measure(Files), items);
    }

    /// <summary>Deletes everything in the Trash for good.</summary>
    public void Empty(string drive)
    {
        foreach (var folder in new[] { Files, Info })
        {
            if (!Directory.Exists(folder))
                continue;
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
            {
                if (entry is DirectoryInfo directory && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    FolderDelete.Run(directory.FullName);
                else
                    entry.Delete();
            }
        }
        var sizes = Path.Combine(trashFolder, "directorysizes");
        if (File.Exists(sizes))
            File.Delete(sizes);
    }

    /// <summary>Moves one file or folder to the Trash. Only works for things on the same drive as your home folder.</summary>
    public void MoveToTrash(string path, DateTime? now = null)
    {
        Directory.CreateDirectory(Files);
        Directory.CreateDirectory(Info);

        var name = Path.GetFileName(path);
        var (info, trashed) = Reserve(name, (now ?? DateTime.Now).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture), path);
        try
        {
            if (Directory.Exists(path))
                Directory.Move(path, trashed);
            else
                File.Move(path, trashed);
        }
        catch (IOException ex)
        {
            File.Delete(info);
            throw new IOException(
                Directory.Exists(path) && !SameDrive(path)
                    ? "It's on a different drive than your Trash. Untick “Move to Trash” to delete it for good."
                    : ex.Message, ex);
        }
    }

    /// <summary>Writes the .trashinfo note first under a free name, so two items never get the same one.</summary>
    private (string Info, string Trashed) Reserve(string name, string deletedAt, string originalPath)
    {
        var note = $"[Trash Info]\nPath={EncodePath(originalPath)}\nDeletionDate={deletedAt}\n";
        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? name : $"{name}.{n}";
            var info = Path.Combine(Info, candidate + ".trashinfo");
            var trashed = Path.Combine(Files, candidate);
            if (File.Exists(trashed) || Directory.Exists(trashed))
                continue;
            try
            {
                using var file = new FileStream(info, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(file);
                writer.Write(note);
                return (info, trashed);
            }
            catch (IOException) when (File.Exists(info))
            {
                // taken; try the next name
            }
        }
    }

    /// <summary>The original path, URL-escaped per folder name as the Trash spec asks.</summary>
    internal static string EncodePath(string path) =>
        string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));

    private bool SameDrive(string path)
    {
        var mounts = LinuxMounts.Read();
        string? DeviceOf(string p) => mounts
            .Where(m => p == m.Target || p.StartsWith(m.Target.TrimEnd('/') + "/", StringComparison.Ordinal))
            .OrderBy(m => m.Target.Length).LastOrDefault()?.Device;
        return mounts.Count == 0 || DeviceOf(path) == DeviceOf(trashFolder);
    }
}
