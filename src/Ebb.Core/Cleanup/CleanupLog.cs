using System.Text.Json;

namespace Ebb.Core.Cleanup;

/// <summary>One thing Ebb did (or tried to do).</summary>
public sealed record LogEntry(
    DateTimeOffset Time,
    string Finding,
    string Action,
    string Target,
    long Bytes,
    bool Succeeded,
    string? Message);

/// <summary>Append-only record of everything Ebb removed or ran, one JSON object per line.</summary>
public sealed class CleanupLog(string filePath)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ebb", "cleanup-log.jsonl");

    public string FilePath => filePath;

    public void Write(LogEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, Json) + Environment.NewLine;
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.AppendAllText(filePath, line);
        }
    }

    public IReadOnlyList<LogEntry> Read()
    {
        lock (_gate)
        {
            if (!File.Exists(filePath))
                return [];
            return File.ReadLines(filePath)
                .Where(line => line.Length > 0)
                .Select(line => JsonSerializer.Deserialize<LogEntry>(line, Json)!)
                .ToList();
        }
    }
}
