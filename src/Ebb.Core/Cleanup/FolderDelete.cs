using System.IO.Enumeration;

namespace Ebb.Core.Cleanup;

/// <summary>
/// Deletes a folder for good, many files at a time. One file at a time, a node_modules folder with
/// hundreds of thousands of small files takes minutes.
/// </summary>
internal static class FolderDelete
{
    private static readonly int Workers = Math.Clamp(Environment.ProcessorCount, 8, 16);

    public static void Run(string path)
    {
        // Renaming fails while anything inside is open (a dev server, an editor), so that case stops before
        // anything is deleted. It also means a project never sees a half-deleted folder.
        var doomed = $"{path}.ebb-deleting-{Guid.NewGuid().ToString("N")[..8]}";
        try
        {
            Directory.Move(path, doomed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException("It's in use, maybe by a dev server or editor. Nothing was deleted.", ex);
        }

        DeleteFiles(doomed);
        DeleteRest(doomed);
    }

    private static void DeleteFiles(string root)
    {
        var files = new FileSystemEnumerable<(string Path, FileAttributes Attributes)>(root,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Attributes),
            new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
            // Folder links (pnpm makes lots) are removed in the last pass, never followed.
            ShouldRecursePredicate = (ref FileSystemEntry entry) => (entry.Attributes & FileAttributes.ReparsePoint) == 0,
        };

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Workers }, file =>
        {
            try
            {
                if (IsPlainReadOnly(file.Attributes))
                    File.SetAttributes(file.Path, FileAttributes.Normal);
                File.Delete(file.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Left for the last pass, which retries it and reports it.
            }
        });
    }

    /// <summary>Removes the empty folders and links that are left, without following links.</summary>
    private static void DeleteRest(string root)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 3)
                    throw new IOException($"Some files were in use, so part of it is left in {root}", ex);
                ClearReadOnly(root);
                Thread.Sleep(200); // antivirus may still be holding a file it just scanned
            }
        }
    }

    private static void ClearReadOnly(string root)
    {
        var readOnly = new FileSystemEnumerable<string>(root, (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => IsPlainReadOnly(entry.Attributes),
            ShouldRecursePredicate = (ref FileSystemEntry entry) => (entry.Attributes & FileAttributes.ReparsePoint) == 0,
        };
        foreach (var path in readOnly.ToList())
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        File.SetAttributes(root, File.GetAttributes(root) & ~FileAttributes.ReadOnly);
    }

    // Links are left alone: changing a link's attributes could change what it points to.
    private static bool IsPlainReadOnly(FileAttributes attributes) =>
        (attributes & (FileAttributes.ReadOnly | FileAttributes.ReparsePoint)) == FileAttributes.ReadOnly;
}
