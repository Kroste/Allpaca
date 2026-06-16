using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class UntrustedTapDetectorTests
{
    [Fact]
    public void Extract_BazziteUblueTap()
    {
        var line = "Error: Refusing to load cask ublue-os/tap/jetbrains-toolbox-linux from untrusted tap ublue-os/tap.";
        Assert.Equal("ublue-os/tap", UntrustedTapDetector.Extract(line));
    }

    [Fact]
    public void Extract_FormulaVariant()
    {
        var line = "Error: Refusing to load formula homebrew/core/some-thing from untrusted tap homebrew/core.";
        Assert.Equal("homebrew/core", UntrustedTapDetector.Extract(line));
    }

    [Fact]
    public void Extract_StopsAtPeriod()
    {
        // Tap-Name darf keinen Trailing-Period mitnehmen.
        var line = "from untrusted tap foo/bar.";
        Assert.Equal("foo/bar", UntrustedTapDetector.Extract(line));
    }

    [Fact]
    public void Extract_StopsAtWhitespace()
    {
        var line = "from untrusted tap foo/bar and more text";
        Assert.Equal("foo/bar", UntrustedTapDetector.Extract(line));
    }

    [Fact]
    public void Extract_ReturnsNull_WhenPatternMissing()
    {
        Assert.Null(UntrustedTapDetector.Extract(""));
        Assert.Null(UntrustedTapDetector.Extract("Just some random log output"));
        Assert.Null(UntrustedTapDetector.Extract("from trusted tap foo/bar.")); // anderes wording
    }

    [Fact]
    public void Extract_ReturnsNull_WhenTapEmpty()
    {
        Assert.Null(UntrustedTapDetector.Extract("from untrusted tap ."));
        Assert.Null(UntrustedTapDetector.Extract("from untrusted tap  "));
    }
}
