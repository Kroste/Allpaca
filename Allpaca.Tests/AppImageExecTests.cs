using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class AppImageExecTests
{
    [Fact]
    public void ExtractExecPath_HandlesQuotedPathWithArgs()
    {
        var exec = "\"/home/lars/Apps/Foo.AppImage\" %U";
        Assert.Equal("/home/lars/Apps/Foo.AppImage", AppImageSource.ExtractExecPath(exec));
    }

    [Fact]
    public void ExtractExecPath_HandlesUnquotedPathWithArgs()
    {
        var exec = "/opt/Bar.AppImage --flag";
        Assert.Equal("/opt/Bar.AppImage", AppImageSource.ExtractExecPath(exec));
    }

    [Fact]
    public void ExtractExecPath_HandlesPlainPath()
    {
        var exec = "/opt/Baz.AppImage";
        Assert.Equal("/opt/Baz.AppImage", AppImageSource.ExtractExecPath(exec));
    }
}
