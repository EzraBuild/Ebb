using System.Runtime.InteropServices;

namespace Ebb.Core.Platform;

internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll", EntryPoint = "GetCompressedFileSizeW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint GetCompressedFileSize(string lpFileName, out uint lpFileSizeHigh);
}
