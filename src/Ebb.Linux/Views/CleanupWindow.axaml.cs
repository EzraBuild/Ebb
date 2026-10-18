using Avalonia.Controls;
using Ebb.Linux.ViewModels;

namespace Ebb.Linux.Views;

public partial class CleanupWindow : Window
{
    private readonly CleanupViewModel _cleanup;

    // For the XAML previewer.
    public CleanupWindow() : this(null!)
    {
    }

    public CleanupWindow(CleanupViewModel cleanup)
    {
        _cleanup = cleanup;
        DataContext = cleanup;
        if (cleanup is not null)
            cleanup.CloseRequested += Close;
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Never walk away from a cleanup half way; the user can stop it after the current step.
        if (_cleanup?.IsRunning == true)
            e.Cancel = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_cleanup is not null)
            _cleanup.CloseRequested -= Close;
        base.OnClosed(e);
    }
}
