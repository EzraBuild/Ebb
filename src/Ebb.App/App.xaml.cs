using System.IO;
using System.Windows;
using System.Windows.Threading;
using Ebb.App.Services;
using Ebb.App.Theme;
using Ebb.App.ViewModels;

namespace Ebb.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        ThemeService.Apply(this);
        ThemeService.FollowSystem(this);

        var window = new MainWindow();
        window.DataContext = new MainViewModel(AppServices.CreateDefault(), new WindowDialogs(window), DriveViewModel.FixedDrives());
        MainWindow = window;
        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ebb", "errors.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.AppendAllText(log, $"{DateTimeOffset.Now:O} {e.Exception}{Environment.NewLine}");
        MessageBox.Show($"Something went wrong: {e.Exception.Message}\n\nDetails are in {log}.", "Ebb");
        e.Handled = true;
    }
}
