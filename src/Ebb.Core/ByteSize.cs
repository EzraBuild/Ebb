using System.Globalization;

namespace Ebb.Core;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Formats bytes the way Explorer does (1024-based): "340 MB", "1.2 GB", "12.5 GB".</summary>
    public static string Format(long bytes, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        double value = bytes;
        var unit = 0;
        while (Math.Abs(value) >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        if (unit == 0)
            return string.Format(provider, "{0} B", bytes);

        var format = Math.Abs(value) >= 100 ? "{0:0} {1}" : "{0:0.#} {1}";
        return string.Format(provider, format, value, Units[unit]);
    }
}
