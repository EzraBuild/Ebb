using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Ebb.Linux.ViewModels;
using Ebb.Linux.Views;

namespace Ebb.Linux.Services;

public sealed class WindowDialogs(Window owner) : IDialogs
{
    public Task ShowCleanupAsync(CleanupViewModel cleanup) => new CleanupWindow(cleanup).ShowDialog(owner);

    public void Open(string target, string? arguments = null)
    {
        // xdg-open hands it to the desktop's default app (the file manager for folders).
        var start = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        start.ArgumentList.Add(target);
        try
        {
            Process.Start(start)?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ErrorLog.Write($"Couldn't open {target}: {ex.Message}");
        }
    }

    public void Reveal(string path) =>
        Open(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
}
