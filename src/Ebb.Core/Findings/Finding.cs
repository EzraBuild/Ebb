namespace Ebb.Core.Findings;

public enum Category
{
    Developer,
    Windows,
    Personal,
}

public enum Safety
{
    /// <summary>Comes back by itself or isn't needed. Fine to remove.</summary>
    Safe,

    /// <summary>Probably fine, but it's yours or it changes something. Look first.</summary>
    CheckFirst,

    /// <summary>Don't remove it. There may still be a safe action (like shrinking a disk).</summary>
    Keep,
}

/// <summary>One concrete file or folder that a finding is made of.</summary>
public sealed record FindingItem(
    string Path,
    long Bytes,
    DateTime LastWriteUtc,
    string? Note = null,
    bool SelectedByDefault = true);

/// <summary>Something using space, explained in plain words.</summary>
public sealed record Finding
{
    /// <summary>Stable id like "dev.cache.npm". Used in the cleanup log.</summary>
    public required string Id { get; init; }

    public required Category Category { get; init; }

    public required string Title { get; init; }

    /// <summary>Why it's there and why it's safe (or not) to remove.</summary>
    public required string Reason { get; init; }

    public required Safety Safety { get; init; }

    /// <summary>Space used, or null when it can't be known up front.</summary>
    public required long? Bytes { get; init; }

    /// <summary>Main location, shown to the user. Null for findings spread over many folders.</summary>
    public string? Path { get; init; }

    public IReadOnlyList<FindingItem> Items { get; init; } = [];

    /// <summary>What cleaning it means. Null when there's nothing to do.</summary>
    public FindingAction? Action { get; init; }
}
