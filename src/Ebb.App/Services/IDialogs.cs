using Ebb.App.ViewModels;

namespace Ebb.App.Services;

public interface IDialogs
{
    void ShowCleanup(CleanupViewModel cleanup);

    /// <summary>Opens a folder, file, settings page or Windows tool the way Explorer would.</summary>
    void Open(string target, string? arguments = null);

    /// <summary>Shows a file selected in Explorer.</summary>
    void Reveal(string path);
}
