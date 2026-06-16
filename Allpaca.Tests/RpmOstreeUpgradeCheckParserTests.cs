using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class RpmOstreeUpgradeCheckParserTests
{
    [Fact]
    public void ExtractAvailableVersion_FullBlock()
    {
        var input = """
        AvailableUpdate:
          Version: 41.20241115.0 (2024-11-15T00:55:09Z)
          Commit: a1b2c3d4e5f6
          GPGSignature: ...
        """;

        var v = RpmOstreeUpgradeCheckParser.ExtractAvailableVersion(input);

        Assert.Equal("41.20241115.0", v);
    }

    [Fact]
    public void ExtractAvailableVersion_WithoutTimestamp()
    {
        var input = "AvailableUpdate:\n  Version: 41.20250115.1\n  Commit: deadbeef";

        var v = RpmOstreeUpgradeCheckParser.ExtractAvailableVersion(input);

        Assert.Equal("41.20250115.1", v);
    }

    [Fact]
    public void ExtractAvailableVersion_ReturnsNull_ForEmptyOrUnrelated()
    {
        Assert.Null(RpmOstreeUpgradeCheckParser.ExtractAvailableVersion(""));
        Assert.Null(RpmOstreeUpgradeCheckParser.ExtractAvailableVersion("   "));
        Assert.Null(RpmOstreeUpgradeCheckParser.ExtractAvailableVersion("No updates available."));
        Assert.Null(RpmOstreeUpgradeCheckParser.ExtractAvailableVersion("Commit: abc\nGPGSignature: x"));
    }

    [Fact]
    public void ExtractAvailableVersion_HandlesCrlf()
    {
        var input = "AvailableUpdate:\r\n  Version: 41.20240901.0 (2024-09-01T12:00:00Z)\r\n";

        var v = RpmOstreeUpgradeCheckParser.ExtractAvailableVersion(input);

        Assert.Equal("41.20240901.0", v);
    }
}
