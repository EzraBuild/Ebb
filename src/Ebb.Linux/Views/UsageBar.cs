using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Ebb.Linux.Views;

/// <summary>The disk bar: used (solid), safe to remove (striped), free (empty).</summary>
public sealed class UsageBar : Control
{
    public static readonly StyledProperty<double> UsedProperty = AvaloniaProperty.Register<UsageBar, double>(nameof(Used));
    public static readonly StyledProperty<double> SafeProperty = AvaloniaProperty.Register<UsageBar, double>(nameof(Safe));
    public static readonly StyledProperty<double> FreeProperty = AvaloniaProperty.Register<UsageBar, double>(nameof(Free));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<UsageBar, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<UsageBar, IBrush?>(nameof(Track));

    static UsageBar() => AffectsRender<UsageBar>(UsedProperty, SafeProperty, FreeProperty, FillProperty, TrackProperty);

    public double Used { get => GetValue(UsedProperty); set => SetValue(UsedProperty, value); }
    public double Safe { get => GetValue(SafeProperty); set => SetValue(SafeProperty, value); }
    public double Free { get => GetValue(FreeProperty); set => SetValue(FreeProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    public override void Render(DrawingContext context)
    {
        var total = Used + Safe + Free;
        if (total <= 0)
            return;

        var (width, height) = (Bounds.Width, Bounds.Height);
        var used = width * Used / total;
        var safe = width * Safe / total;
        context.FillRectangle(Fill ?? Brushes.Black, new Rect(0, 0, used, height));
        context.FillRectangle(Track ?? Brushes.Gray, new Rect(used + safe, 0, Math.Max(0, width - used - safe), height));

        // Diagonal stripes, 6 px apart, clipped to the safe part.
        using (context.PushClip(new Rect(used, 0, safe, height)))
        {
            var pen = new Pen(Fill ?? Brushes.Black, 1.2);
            for (var x = used - height; x < used + safe + height; x += 6)
                context.DrawLine(pen, new Point(x, height), new Point(x + height, 0));
        }
    }
}
