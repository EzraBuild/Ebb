using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Ebb.Core.Platform;
using Ebb.Linux.Services;
using Ebb.Linux.ViewModels;
using Ebb.Linux.Views;

namespace Ebb.Linux;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            ErrorLog.Write(e.Exception);
            e.Handled = true;
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mounts = LinuxMounts.Read();
            var window = new MainWindow();
            window.DataContext = new MainViewModel(AppServices.CreateDefault(mounts), new WindowDialogs(window),
                DriveViewModel.Mounted(mounts));
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
