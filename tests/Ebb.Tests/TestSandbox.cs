using System.Diagnostics;

namespace Ebb.Tests;

/// <summary>
/// A throwaway folder under %TEMP%\EbbTests. Every test that touches the disk works only in here,
/// never on real user files.
/// </summary>
internal sealed class TestSandbox : IDisposable
{
    public TestSandbox()
    {
        Root = Path.Combine(Path.GetTempPath(), "EbbTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathOf(string relative) => Path.Combine(Root, relative);

    /// <summary>Creates a file of <paramref name="bytes"/> bytes (filled with a repeating pattern).</summary>
    public string File(string relative, long bytes = 0, DateTime? lastWriteUtc = null, byte fill = 0x2A)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            var chunk = new byte[Math.Min(bytes, 1 << 20)];
            Array.Fill(chunk, fill);
            for (var left = bytes; left > 0; left -= chunk.Length)
                stream.Write(chunk, 0, (int)Math.Min(chunk.Length, left));
        }
        if (lastWriteUtc is { } time)
            System.IO.File.SetLastWriteTimeUtc(path, time);
        return path;
    }

    public string File(string relative, byte[] content, DateTime? lastWriteUtc = null)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, content);
        if (lastWriteUtc is { } time)
            System.IO.File.SetLastWriteTimeUtc(path, time);
        return path;
    }

    public string Dir(string relative, DateTime? lastWriteUtc = null)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(path);
        if (lastWriteUtc is { } time)
            Directory.SetLastWriteTimeUtc(path, time);
        return path;
    }

    /// <summary>Makes a directory junction (no admin needed).</summary>
    public string Junction(string relative, string target)
    {
        var path = PathOf(relative);
        var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{path}\" \"{target}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        mklink.WaitForExit();
        Assert.True(Directory.Exists(path), "Could not create junction");
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
