using System.Runtime.InteropServices;

namespace Ebb.Core.Platform;

public sealed record RecycleBinInfo(long Bytes, long Items);

public interface IRecycleBin
{
    RecycleBinInfo Query(string drive);

    /// <summary>Empties the bin for one drive. Can't be undone.</summary>
    void Empty(string drive);
}

public sealed class ShellRecycleBin : IRecycleBin
{
    private const int E_UNEXPECTED = unchecked((int)0x8000FFFF); // returned when the bin is already empty

    public RecycleBinInfo Query(string drive)
    {
        var info = new NativeMethods.SHQUERYRBINFO { cbSize = Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>() };
        var hr = NativeMethods.SHQueryRecycleBin(drive, ref info);
        return hr == 0 ? new RecycleBinInfo(info.i64Size, info.i64NumItems) : new RecycleBinInfo(0, 0);
    }

    public void Empty(string drive)
    {
        var hr = NativeMethods.SHEmptyRecycleBin(0, drive,
            NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
        if (hr != 0 && hr != E_UNEXPECTED)
            Marshal.ThrowExceptionForHR(hr);
    }
}
