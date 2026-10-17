using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Ebb.App.Theme;

/// <summary>Follows Windows' light/dark setting with Ebb's two monochrome palettes.</summary>
public static class ThemeService
{
    /// <param name="light">Force a palette; by default follows Windows.</param>
    public static void Apply(Application app, bool? light = null)
    {
        light ??= IsLight();

        // WPF-UI first (it swaps its own dictionaries), then Ebb's palette on top.
        ApplicationThemeManager.Apply(light.Value ? ApplicationTheme.Light : ApplicationTheme.Dark, WindowBackdropType.None, false);

        var palette = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Ebb;component/Theme/Palette.{(light.Value ? "Light" : "Dark")}.xaml"),
        };
        var merged = app.Resources.MergedDictionaries;
        var current = merged.FirstOrDefault(d => d.Contains("Ebb.Bg"));
        if (current is null)
            merged.Add(palette);
        else
            merged[merged.IndexOf(current)] = palette;
    }

    public static void FollowSystem(Application app) =>
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
                app.Dispatcher.Invoke(() => Apply(app));
        };

    private static bool IsLight() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 0) is 1;
}
