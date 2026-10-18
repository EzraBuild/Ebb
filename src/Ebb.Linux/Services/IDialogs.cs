using Ebb.Linux.ViewModels;

namespace Ebb.Linux.Services;

public interface IDialogs
{
    /// <summary>Shows the review window and waits until it's closed.</summary>
    Task ShowCleanupAsync(CleanupViewModel cleanup);

    /// <summary>Opens a folder or file with the desktop's default app.</summary>
    void Open(string target, string? arguments = null);

    /// <summary>Opens the folder a file is in.</summary>
    void Reveal(string path);
}
