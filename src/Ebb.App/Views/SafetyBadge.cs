using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Ebb.Core.Findings;

namespace Ebb.App.Views;

/// <summary>
/// SAFE is filled, CHECK FIRST is outlined, KEEP is dashed: three looks that work without color.
/// </summary>
public sealed class SafetyBadge : Border
{
    public static readonly DependencyProperty SafetyProperty = DependencyProperty.Register(
        nameof(Safety), typeof(Safety), typeof(SafetyBadge),
        new PropertyMetadata(Safety.Safe, (badge, _) => ((SafetyBadge)badge).Update()));

    private readonly TextBlock _text = new() { FontSize = 10, Margin = new Thickness(6, 1, 6, 1) };
    private readonly Rectangle _dashes = new() { StrokeThickness = 1, StrokeDashArray = [3, 2], SnapsToDevicePixels = true };

    public SafetyBadge()
    {
        BorderThickness = new Thickness(1);
        SnapsToDevicePixels = true;
        VerticalAlignment = VerticalAlignment.Center;
        _text.SetResourceReference(TextBlock.FontFamilyProperty, "Ebb.Mono");
        Child = new Grid { Children = { _dashes, _text } };
        Update();
    }

    public Safety Safety
    {
        get => (Safety)GetValue(SafetyProperty);
        set => SetValue(SafetyProperty, value);
    }

    private void Update()
    {
        _text.Text = ViewModels.Display.SafetyLabel(Safety);
        _dashes.Visibility = Safety == Safety.Keep ? Visibility.Visible : Visibility.Collapsed;
        _dashes.SetResourceReference(Shape.StrokeProperty, "Ebb.TextMuted");

        switch (Safety)
        {
            case Safety.Safe:
                SetResourceReference(BackgroundProperty, "Ebb.Inverse");
                SetResourceReference(BorderBrushProperty, "Ebb.Inverse");
                _text.SetResourceReference(TextBlock.ForegroundProperty, "Ebb.InverseText");
                break;
            case Safety.CheckFirst:
                Background = Brushes.Transparent;
                SetResourceReference(BorderBrushProperty, "Ebb.Text");
                _text.SetResourceReference(TextBlock.ForegroundProperty, "Ebb.Text");
                break;
            default:
                Background = Brushes.Transparent;
                BorderBrush = Brushes.Transparent;
                _text.SetResourceReference(TextBlock.ForegroundProperty, "Ebb.TextMuted");
                break;
        }
    }
}
