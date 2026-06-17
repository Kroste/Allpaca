using Allpaca.Services;
using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class IconLookupTests
{
    [Fact]
    public void FindPng_ReturnsNull_ForEmptyOrWhitespace()
    {
        Assert.Null(IconLookup.FindPng(""));
        Assert.Null(IconLookup.FindPng("   "));
    }

    [Fact]
    public void FindPng_ReturnsNull_ForObviouslyMissingId()
    {
        // Eine ID, die garantiert in keinem hicolor-Pfad existiert.
        var result = IconLookup.FindPng("definitely.not.a.real.app.guid-" + System.Guid.NewGuid().ToString("N"));
        Assert.Null(result);
    }

    [Fact]
    public void FindPng_FindsIconInTempHicolorTree()
    {
        // Wir bauen uns einen Mini-hicolor-Baum unter $XDG_DATA_HOME-aequivalent
        // (~/.local/share/icons/hicolor) und pruefen, ob FindPng ihn findet.
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var id = "allpaca-test-" + System.Guid.NewGuid().ToString("N");
        var dir = Path.Combine(home, ".local/share/icons/hicolor/128x128/apps");
        var iconFile = Path.Combine(dir, id + ".png");

        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(iconFile, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG-Magic, reicht als Test-Sentinel

            var result = IconLookup.FindPng(id);

            Assert.Equal(iconFile, result);
        }
        finally
        {
            if (File.Exists(iconFile)) File.Delete(iconFile);
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
            // ResolveIcon ist internal - direkter Aufruf reicht ueber InternalsVisibleTo.
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
