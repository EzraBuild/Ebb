using System.IO.Enumeration;
using System.Security.Cryptography;
using Ebb.Core.Scanning;

namespace Ebb.Core.Detectors;

public sealed record FileRecord(string Path, long Size, DateTime LastWriteUtc);

/// <summary>Files with the same content. <see cref="Files"/>[0] is the one to keep (the oldest).</summary>
public sealed record DuplicateGroup(long Size, IReadOnlyList<FileRecord> Files)
{
    public long WastedBytes => Size * (Files.Count - 1);
}

/// <summary>
/// Finds exact duplicates: same size, then same first 64 KB, then same full SHA-256.
/// Skips online-only cloud files so nothing gets downloaded.
/// </summary>
public sealed class DuplicateFinder(long minSize = 1024 * 1024)
{
    private const int HeadBytes = 64 * 1024;

    public IReadOnlyList<DuplicateGroup> Find(IEnumerable<string> roots, CancellationToken cancellationToken = default)
    {
        var files = new Dictionary<string, FileRecord>(PathUtil.Comparer);
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Enumerate(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                files.TryAdd(file.Path, file);
            }
        }

        var groups = new List<DuplicateGroup>();
        foreach (var sameSize in files.Values.GroupBy(f => f.Size).Where(g => g.Count() > 1))
        {
            foreach (var sameHead in GroupByHash(sameSize, HashHead, cancellationToken))
            {
                IEnumerable<List<FileRecord>> matches = sameSize.Key <= HeadBytes ? [sameHead] : GroupByHash(sameHead, HashAll, cancellationToken);
                foreach (var match in matches)
                {
                    var ordered = match.OrderBy(f => f.LastWriteUtc).ThenBy(f => f.Path.Length).ThenBy(f => f.Path).ToList();
                    groups.Add(new DuplicateGroup(sameSize.Key, ordered));
                }
            }
        }
        return groups.OrderByDescending(g => g.WastedBytes).ToList();
    }

    private IEnumerable<FileRecord> Enumerate(string root)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return new FileSystemEnumerable<FileRecord>(root,
            (ref FileSystemEntry e) => new FileRecord(e.ToFullPath(), e.Length, e.LastWriteTimeUtc.UtcDateTime), options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry e) =>
                !e.IsDirectory && e.Length >= minSize && !FileSizes.IsCloudOnly(e.Attributes) &&
                (e.Attributes & FileAttributes.ReparsePoint) == 0,
            ShouldRecursePredicate = (ref FileSystemEntry e) =>
                (e.Attributes & FileAttributes.ReparsePoint) == 0 && !IsToolFolder(e.FileName),
        };
    }

    private static readonly HashSet<string> ToolFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "packages", "venv", "__pycache__", "target",
    };

    // Build output, packages and dot folders (.git, .vs, .venv) are full of identical files on purpose.
    // They aren't personal files, and other findings cover them.
    private static bool IsToolFolder(ReadOnlySpan<char> name) =>
        (name.Length > 0 && name[0] == '.') || ToolFolders.Contains(name.ToString());

    private static IEnumerable<List<FileRecord>> GroupByHash(IEnumerable<FileRecord> files, Func<string, string?> hash,
        CancellationToken cancellationToken) =>
        files
            .Select(f =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return (File: f, Hash: hash(f.Path));
            })
            .Where(x => x.Hash is not null)
            .GroupBy(x => x.Hash)
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(x => x.File).ToList())
            .ToList();

    private static string? HashHead(string path) => Hash(path, HeadBytes);

    private static string? HashAll(string path) => Hash(path, long.MaxValue);

    private static string? Hash(string path, long limit)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                1 << 20, FileOptions.SequentialScan);
            if (limit >= stream.Length)
                return Convert.ToHexString(SHA256.HashData(stream));

            var head = new byte[limit];
            stream.ReadExactly(head);
            return Convert.ToHexString(SHA256.HashData(head));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null; // in use or protected: never report what we couldn't read
        }
    }
}
