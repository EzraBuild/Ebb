namespace Ebb.Linux.Services;

/// <summary>Writes crashes to ~/.local/share/Ebb/errors.log.</summary>
internal static class ErrorLog
{
    public static void Write(object error)
    {
        try
        {
            var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ebb", "errors.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTimeOffset.Now:O} {error}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
