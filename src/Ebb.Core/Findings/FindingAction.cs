using Ebb.Core.Elevation;

namespace Ebb.Core.Findings;

public abstract record FindingAction;

/// <summary>Removes the finding's items (to the Recycle Bin unless the user opts out).</summary>
/// <param name="RebuiltBy">
/// For things a tool brings back, like node_modules: how. These are deleted for good instead. Sending that
/// many files to the Recycle Bin is slow, they often don't fit, and it frees nothing until the bin is emptied.
/// </param>
public sealed record RemoveItems(string? RebuiltBy = null) : FindingAction;

/// <summary>Runs the tool's own clean command. If the tool isn't installed, the items are removed instead.</summary>
/// <param name="AsRoot">Linux: runs it as root through pkexec, which asks for your password first.</param>
public sealed record RunTool(string Tool, IReadOnlyList<string> Arguments, bool AsRoot = false) : FindingAction
{
    public string CommandLine => string.Join(' ', [.. AsRoot ? ["pkexec"] : Array.Empty<string>(), Tool, .. Arguments]);
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
