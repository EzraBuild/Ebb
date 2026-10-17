using Ebb.Core.Elevation;

namespace Ebb.Core.Findings;

public abstract record FindingAction;

/// <summary>Removes the finding's items (to the Recycle Bin unless the user opts out).</summary>
public sealed record RemoveItems : FindingAction;

/// <summary>Runs the tool's own clean command. If the tool isn't installed, the items are removed instead.</summary>
public sealed record RunTool(string Tool, IReadOnlyList<string> Arguments) : FindingAction
{
    public string CommandLine => string.Join(' ', [Tool, .. Arguments]);
}

/// <summary>A Windows command that needs admin, run by Ebb.Helper.</summary>
public sealed record RunElevated(HelperOperation Operation) : FindingAction;

public sealed record EmptyRecycleBin(string Drive) : FindingAction;

public enum DiskOwner
{
    Wsl,
    Docker,
}

/// <summary>Shrinks a WSL or Docker virtual disk. Deletes nothing.</summary>
public sealed record CompactDisk(string VhdxPath, DiskOwner Owner, string? DistroName) : FindingAction;

/// <summary>Ebb doesn't do this itself: it opens the right Windows tool, setting or folder.</summary>
public sealed record OpenTarget(string Target, string? Arguments, string Label) : FindingAction;
