using System.ComponentModel;
using Ebb.App.ViewModels;
using Wpf.Ui.Controls;

namespace Ebb.App.Views;

public partial class CleanupWindow : FluentWindow
{
    private readonly CleanupViewModel _cleanup;

    public CleanupWindow(CleanupViewModel cleanup)
    {
        _cleanup = cleanup;
        DataContext = cleanup;
        cleanup.CloseRequested += Close;
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Never walk away from a cleanup half way; the user can stop it after the current step.
        if (_cleanup.IsRunning)
            e.Cancel = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _cleanup.CloseRequested -= Close;
        base.OnClosed(e);
    }
}
