using Ebb.Core.Findings;

namespace Ebb.Core.Vhdx;

public sealed record CompactResult(bool Succeeded, string Message, long BytesBefore, long BytesAfter)
{
    public long Freed => Math.Max(0, BytesBefore - BytesAfter);
}

/// <summary>Shrinks a WSL or Docker virtual disk file.</summary>
public interface IDiskCompactor
{
    Task<CompactResult> CompactAsync(CompactDisk disk, CancellationToken cancellationToken = default);
}
