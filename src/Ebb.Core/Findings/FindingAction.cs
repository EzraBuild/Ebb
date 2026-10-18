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
/// <param name="Name">What to call it in messages, when the tool is a shell running the real one (like snap).</param>
public sealed record RunTool(string Tool, IReadOnlyList<string> Arguments, bool AsRoot = false, string? Name = null) : FindingAction
{
    public string DisplayName => Name ?? Tool;

    /// <summary>The command as you'd type it.</summary>
    public string Command => string.Join(' ', [Tool, .. Arguments.Select(Quote)]);

    /// <summary>The command, with pkexec in front when it runs as root.</summary>
    public string CommandLine => AsRoot ? "pkexec " + Command : Command;

    private static string Quote(string argument) => argument.Contains(' ') ? $"'{argument}'" : argument;
}

/// <summary>A Windows command that needs admin, run by Ebb.Helper.</summary>
public sealed record RunElevated(HelperOperation Operation) : FindingAction;

/// <param name="IsTrash">Linux: it's the Trash in your home folder, not a Windows Recycle Bin.</param>
public sealed record EmptyRecycleBin(string Drive, bool IsTrash = false) : FindingAction;

public enum DiskOwner
{
    Wsl,
    Docker,
}

/// <summary>Shrinks a WSL or Docker virtual disk. Deletes nothing.</summary>
public sealed record CompactDisk(string VhdxPath, DiskOwner Owner, string? DistroName) : FindingAction;

/// <summary>Ebb doesn't do this itself: it opens the right Windows tool, setting or folder.</summary>
public sealed record OpenTarget(string Target, string? Arguments, string Label) : FindingAction;
