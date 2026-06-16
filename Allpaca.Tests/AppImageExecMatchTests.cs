using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class AppImageExecMatchTests
{
    [Theory]
    [InlineData("/home/u/Apps/Brave.AppImage", "/home/u/Apps/Brave.AppImage", true)]
    [InlineData("/home/u/Apps/Brave.AppImage %U", "/home/u/Apps/Brave.AppImage", true)]
    [InlineData("\"/home/u/Apps/My App.AppImage\"", "/home/u/Apps/My App.AppImage", true)]
    [InlineData("\"/home/u/Apps/My App.AppImage\" %F", "/home/u/Apps/My App.AppImage", true)]
    [InlineData("/home/u/Apps/Other.AppImage", "/home/u/Apps/Brave.AppImage", false)]
    [InlineData("/home/u/Apps/Brave.AppImage", "/home/u/Apps/brave.appimage", false)]  // case matters
    public void ExecMatchesAppImage_HonorsPathExactly(string exec, string appImage, bool expected)
    {
        Assert.Equal(expected, AppImageSource.ExecMatchesAppImage(exec, appImage));
    }

    [Fact]
    public void ExecMatchesAppImage_NullExec_IsFalse()
    {
        Assert.False(AppImageSource.ExecMatchesAppImage(null, "/anything"));
    }
}
