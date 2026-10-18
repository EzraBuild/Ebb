using Avalonia;
using Ebb.Linux.Services;

namespace Ebb.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (FontConfig.IsCacheBuilder(args))
        {
            FontConfig.BuildCache();
            return 0;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => ErrorLog.Write(e.ExceptionObject);
        FontConfig.EnsureCache();
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { WmClass = "ebb" }) // matches ebb.desktop, so docks show the right icon
            .LogToTrace();
}
