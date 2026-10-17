using System.Diagnostics;
using System.IO;
using System.Windows;
using Ebb.App.ViewModels;
using Ebb.App.Views;

namespace Ebb.App.Services;

public sealed class WindowDialogs(Window owner) : IDialogs
{
    public void ShowCleanup(CleanupViewModel cleanup) =>
        new CleanupWindow(cleanup) { Owner = owner }.ShowDialog();

    public void Open(string target, string? arguments = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target, arguments ?? "") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            MessageBox.Show(owner, $"Couldn't open {target}.\n\n{ex.Message}", "Ebb");
        }
    }

    public void Reveal(string path)
    {
        if (File.Exists(path))
            Open("explorer.exe", $"/select,\"{path}\"");
        else
            Open(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
    }
}
