using System.Globalization;
using Ebb.Core;

namespace Ebb.Tests;

public class PathAndSizeTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(340L * 1024 * 1024, "340 MB")]
    [InlineData(1288490189L, "1.2 GB")]
    [InlineData(13421772800L, "12.5 GB")]
    public void Formats_sizes(long bytes, string expected) =>
        Assert.Equal(expected, ByteSize.Format(bytes, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData(@"C:\a\b", @"C:\a", true)]
    [InlineData(@"C:\A\B\", @"c:\a", true)]
    [InlineData(@"C:\a", @"C:\a", false)]
    [InlineData(@"C:\ab", @"C:\a", false)]
    [InlineData(@"C:\a", @"C:\", true)]
    [InlineData(@"D:\a", @"C:\", false)]
    public void IsUnder_is_strict_and_case_insensitive(string path, string folder, bool expected) =>
        Assert.Equal(expected, PathUtil.IsUnder(path, folder));

    [Fact]
    public void Normalize_keeps_drive_roots_and_trims_others()
    {
        Assert.Equal(@"C:\", PathUtil.Normalize(@"C:\"));
        Assert.Equal(@"C:\a", PathUtil.Normalize(@"C:\a\"));
        Assert.Equal(@"C:\a\c", PathUtil.Normalize(@"C:\a\b\..\c"));
    }
}
