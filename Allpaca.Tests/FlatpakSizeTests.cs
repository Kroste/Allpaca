using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class FlatpakSizeTests
{
    [Theory]
    [InlineData("234 MB", 234L * 1024 * 1024)]
    [InlineData("1.2 GB", (long)(1.2 * 1024 * 1024 * 1024))]
    [InlineData("1,2 GB", (long)(1.2 * 1024 * 1024 * 1024))]   // deutsche Locale
    [InlineData("512 kB", 512L * 1024)]
    public void ParseSize_ParsesCommonFormats(string input, long expected)
    {
        var result = FlatpakSource.ParseSize(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kaputt")]
    [InlineData("123")]            // ohne Einheit
    public void ParseSize_ReturnsNull_OnInvalid(string input)
    {
        Assert.Null(FlatpakSource.ParseSize(input));
    }
}
