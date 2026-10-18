using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ebb.Linux.Services;

/// <summary>
/// Makes sure fontconfig has a font cache before Avalonia starts. On an account without one yet
/// (~/.cache/fontconfig), fontconfig 2.17 builds it inside the app on first use, and the app then crashes
/// with "free(): invalid pointer". A cache built by another process avoids that, so Ebb starts a short
/// copy of itself that only builds the cache, and waits for it.
/// </summary>
internal static class FontConfig
{
    private const string BuildCacheArgument = "--build-font-cache";

    /// <summary>True when this process is that short copy. It builds the cache and should exit.</summary>
    public static bool IsCacheBuilder(string[] args) => args is [BuildCacheArgument];

    public static void BuildCache()
    {
        try
        {
            FcInit();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // No fontconfig: Avalonia will report that itself.
        }
    }

    public static void EnsureCache()
    {
        if (!OperatingSystem.IsLinux() || HasUserCache() || Environment.ProcessPath is not { } self)
            return;

        var start = new ProcessStartInfo(self) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self) == "dotnet")
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location); // started as "dotnet ebb.dll"
        start.ArgumentList.Add(BuildCacheArgument);
        try
        {
            using var builder = Process.Start(start);
            builder?.WaitForExit(30_000);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
        }
    }

    private static bool HasUserCache()
    {
        var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify), ".cache");
        var folder = Path.Combine(cache, "fontconfig");
        return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.cache-*").Any();
    }

    [DllImport("libfontconfig.so.1")]
    private static extern int FcInit();
}
