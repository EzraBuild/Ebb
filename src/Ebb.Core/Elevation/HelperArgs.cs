using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Ebb.Core.Elevation;

/// <summary>How Ebb talks to Ebb.Helper.exe: a fixed command line in, a small JSON file out.</summary>
public static class HelperArgs
{
    private static readonly Dictionary<string, HelperOperation> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["compact-vhdx"] = HelperOperation.CompactVhdx,
        ["hibernate-off"] = HelperOperation.HibernateOff,
        ["component-cleanup"] = HelperOperation.ComponentCleanup,
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> Build(HelperRequest request, string resultPath)
    {
        var name = Names.Single(n => n.Value == request.Operation).Key;
        return request.Path is null
            ? [name, "--result", resultPath]
            : [name, "--path", request.Path, "--result", resultPath];
    }

    public static bool TryParse(string[] args, [NotNullWhen(true)] out HelperRequest? request,
        [NotNullWhen(true)] out string? resultPath, out string? error)
    {
        request = null;
        resultPath = Option(args, "--result");
        error = null;

        if (resultPath is null || !Path.IsPathFullyQualified(resultPath) ||
            !Path.GetFileName(resultPath).StartsWith("ebb-helper-", StringComparison.Ordinal) ||
            !resultPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            resultPath = null;
            error = "Missing or invalid --result path.";
            return false;
        }

        if (args.Length == 0 || !Names.TryGetValue(args[0], out var operation))
        {
            error = "Unknown operation.";
            return false;
        }

        var path = Option(args, "--path");
        if (operation == HelperOperation.CompactVhdx && path is null)
        {
            error = "compact-vhdx needs --path.";
            return false;
        }

        request = new HelperRequest(operation, path);
        return true;
    }

    public static void WriteResult(string resultPath, HelperResult result) =>
        File.WriteAllText(resultPath, JsonSerializer.Serialize(result, Json));

    public static HelperResult ReadResult(string resultPath) =>
        JsonSerializer.Deserialize<HelperResult>(File.ReadAllText(resultPath), Json)!;

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
