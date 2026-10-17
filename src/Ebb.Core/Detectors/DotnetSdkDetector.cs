using Ebb.Core.Findings;

namespace Ebb.Core.Detectors;

/// <summary>Older .NET SDKs. Ebb never deletes Program Files; it points you to Installed apps.</summary>
public sealed class DotnetSdkDetector : Detector
{
    protected override IEnumerable<Finding> Detect(ScanContext context, CancellationToken cancellationToken)
    {
        var sdkRoot = Path.Combine(context.Paths.ProgramFiles, "dotnet", "sdk");
        if (!Directory.Exists(sdkRoot))
            yield break;

        var sdks = Directory.EnumerateDirectories(sdkRoot)
            .Select(dir => (Path: dir, Version: SdkVersion.TryParse(Path.GetFileName(dir))))
            .Where(s => s.Version is not null)
            .Select(s => (s.Path, Version: s.Version!.Value))
            .ToList();
        var newestPerMajor = sdks.GroupBy(s => s.Version.Major).Select(g => g.Max(s => s.Version)).ToHashSet();

        var old = sdks
            .Where(s => !newestPerMajor.Contains(s.Version))
            .OrderBy(s => s.Version)
            .Select(s => new FindingItem(s.Path, context.SizeOf(s.Path), Directory.GetLastWriteTimeUtc(s.Path),
                $".NET SDK {s.Version}", SelectedByDefault: false))
            .ToList();
        if (old.Count == 0)
            yield break;

        yield return new Finding
        {
            Id = "dev.dotnet-sdks",
            Category = Category.Developer,
            Title = old.Count == 1 ? "Older .NET SDK" : $"{old.Count} older .NET SDKs",
            Reason = "The newest SDK for each .NET version is kept. Remove old ones from Installed apps (search " +
                     "“.NET SDK”). A project with a global.json file may need a specific version.",
            Safety = Safety.CheckFirst,
            Bytes = old.Sum(i => i.Bytes),
            Path = sdkRoot,
            Items = old,
            Action = new OpenTarget("ms-settings:appsfeatures", null, "Open Installed apps"),
        };
    }
}

/// <summary>An SDK folder name like "8.0.411" or "9.0.100-preview.7". Releases sort above previews.</summary>
public readonly record struct SdkVersion(Version Number, string? Preview) : IComparable<SdkVersion>
{
    public int Major => Number.Major;

    public static SdkVersion? TryParse(string text)
    {
        var dash = text.IndexOf('-');
        var number = dash < 0 ? text : text[..dash];
        return Version.TryParse(number, out var version)
            ? new SdkVersion(version, dash < 0 ? null : text[(dash + 1)..])
            : null;
    }

    public int CompareTo(SdkVersion other)
    {
        var byNumber = Number.CompareTo(other.Number);
        if (byNumber != 0)
            return byNumber;
        if (Preview is null || other.Preview is null)
            return (Preview is null ? 1 : 0) - (other.Preview is null ? 1 : 0);
        return string.CompareOrdinal(Preview, other.Preview);
    }

    public override string ToString() => Preview is null ? Number.ToString() : $"{Number}-{Preview}";
}
