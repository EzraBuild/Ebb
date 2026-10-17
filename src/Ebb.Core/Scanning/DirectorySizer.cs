using System.IO.Enumeration;

namespace Ebb.Core.Scanning;

/// <summary>Measures one file or folder directly, without a full scan.</summary>
public static class DirectorySizer
{
    public static long Measure(string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
            return FileSizes.OnDisk(new FileInfo(path));

        var folder = new DirectoryInfo(path);
        if (!folder.Exists || folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return 0; // a link takes no space itself; its target is counted where it lives

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
        };
        var sizes = new FileSystemEnumerable<long>(path, (ref FileSystemEntry e) => FileSizes.OnDisk(ref e), options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry e) => !e.IsDirectory,
            ShouldRecursePredicate = (ref FileSystemEntry e) => (e.Attributes & FileAttributes.ReparsePoint) == 0,
        };

        long total = 0;
        foreach (var size in sizes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += size;
        }
        return total;
    }
}
