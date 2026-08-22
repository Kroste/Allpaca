using Allpaca.Services;
using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class IconLookupTests
{
    [Fact]
    public void FindIcon_ReturnsNull_ForEmptyOrWhitespace()
    {
        Assert.Null(IconLookup.FindIcon(""));
        Assert.Null(IconLookup.FindIcon("   "));
    }

    [Fact]
    public void FindIcon_ReturnsNull_ForObviouslyMissingId()
    {
        var result = IconLookup.FindIcon("definitely.not.a.real.app.guid-" + System.Guid.NewGuid().ToString("N"));
        Assert.Null(result);
    }

    [Fact]
    public void FindIcon_FindsPngInTempHicolorTree()
    {
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var id = "allpaca-test-" + System.Guid.NewGuid().ToString("N");
        var dir = Path.Combine(home, ".local/share/icons/hicolor/128x128/apps");
        var iconFile = Path.Combine(dir, id + ".png");

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(iconFile, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            Assert.Equal(iconFile, IconLookup.FindIcon(id));
        }
        finally { if (File.Exists(iconFile)) File.Delete(iconFile); }
    }

    [Fact]
    public void FindIcon_FallsBackToScalableSvg_WhenNoPng()
    {
        // Brave/Bottles/Brief auf Bazzite haben nur scalable/<id>.svg.
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var id = "allpaca-svg-test-" + System.Guid.NewGuid().ToString("N");
        var dir = Path.Combine(home, ".local/share/icons/hicolor/scalable/apps");
        var iconFile = Path.Combine(dir, id + ".svg");

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(iconFile, "<svg/>");

            Assert.Equal(iconFile, IconLookup.FindIcon(id));
        }
        finally { if (File.Exists(iconFile)) File.Delete(iconFile); }
    }

    [Fact]
    public void FindIcon_PrefersPngOverSvg()
    {
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var id = "allpaca-pref-test-" + System.Guid.NewGuid().ToString("N");
        var pngDir = Path.Combine(home, ".local/share/icons/hicolor/128x128/apps");
        var svgDir = Path.Combine(home, ".local/share/icons/hicolor/scalable/apps");
        var pngFile = Path.Combine(pngDir, id + ".png");
        var svgFile = Path.Combine(svgDir, id + ".svg");

        try
        {
            Directory.CreateDirectory(pngDir);
            Directory.CreateDirectory(svgDir);
            File.WriteAllBytes(pngFile, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            File.WriteAllText(svgFile, "<svg/>");

            Assert.Equal(pngFile, IconLookup.FindIcon(id));
        }
        finally
        {
            if (File.Exists(pngFile)) File.Delete(pngFile);
            if (File.Exists(svgFile)) File.Delete(svgFile);
        }
    }
}

public class AppImageResolveIconTests
{
    [Fact]
    public void ResolveIcon_AbsolutePath_ReturnsPathIfExists()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            // ResolveIcon ist internal - direkter Aufruf reicht über InternalsVisibleTo.
            var result = AppImageSource.ResolveIcon(tmp);
            Assert.Equal(tmp, result);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact]
    public void ResolveIcon_AbsolutePath_ReturnsNullIfMissing()
    {
        var result = AppImageSource.ResolveIcon("/nonexistent/icon/" + System.Guid.NewGuid().ToString("N") + ".png");
        Assert.Null(result);
    }

    [Fact]
    public void ResolveIcon_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(AppImageSource.ResolveIcon(null));
        Assert.Null(AppImageSource.ResolveIcon(""));
        Assert.Null(AppImageSource.ResolveIcon("   "));
    }
}
