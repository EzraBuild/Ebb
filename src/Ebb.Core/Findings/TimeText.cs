namespace Ebb.Core.Findings;

public static class TimeText
{
    public static string Ago(DateTime whenUtc, DateTime nowUtc)
    {
        var days = (int)(nowUtc - whenUtc).TotalDays;
        return days switch
        {
            < 1 => "today",
            1 => "yesterday",
            < 31 => $"{days} days ago",
            < 365 => Plural(days / 30, "month"),
            _ => Plural(days / 365, "year"),
        };
    }

    private static string Plural(int n, string unit) => n == 1 ? $"1 {unit} ago" : $"{n} {unit}s ago";
}
