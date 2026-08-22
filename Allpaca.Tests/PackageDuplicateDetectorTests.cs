using Allpaca.Models;
using Allpaca.ViewModels;
using Xunit;

namespace Allpaca.Tests;

public class PackageDuplicateDetectorTests
{
    private static PackageItemViewModel Pkg(string name, PackageSourceKind src, bool isRuntime = false)
        => new(new PackageInfo { Id = name, Name = name, Source = src, IsRuntime = isRuntime });

    [Fact]
    public void Annotate_MarksSameNameInTwoSources()
    {
        var items = new[]
        {
            Pkg("Brave", PackageSourceKind.Flatpak),
            Pkg("Brave", PackageSourceKind.AppImage),
            Pkg("htop", PackageSourceKind.Homebrew),
        };

        PackageDuplicateDetector.Annotate(items);

        Assert.True(items[0].IsDuplicate);
        Assert.True(items[1].IsDuplicate);
        Assert.False(items[2].IsDuplicate);
        Assert.Equal("AppImage", items[0].DuplicateInfo);
        Assert.Equal("Flatpak", items[1].DuplicateInfo);
    }

    [Fact]
    public void Annotate_NormalizesCaseAndWhitespace()
    {
        var items = new[]
        {
            Pkg("Discord", PackageSourceKind.Flatpak),
            Pkg("DISCORD", PackageSourceKind.AppImage),
        };

        PackageDuplicateDetector.Annotate(items);

        Assert.True(items[0].IsDuplicate);
        Assert.True(items[1].IsDuplicate);
    }

    [Fact]
    public void Annotate_DoesNotFlag_SameNameSameSource()
    {
        // Zwei Einträge mit gleichem Namen aus DERSELBEN Quelle - das ist kein
        // Cross-Source-Duplikat und soll nicht gewarnt werden.
        var items = new[]
        {
            Pkg("zlib", PackageSourceKind.Homebrew),
            Pkg("zlib", PackageSourceKind.Homebrew),
        };

        PackageDuplicateDetector.Annotate(items);

        Assert.False(items[0].IsDuplicate);
        Assert.False(items[1].IsDuplicate);
    }

    [Fact]
    public void Annotate_SkipsRuntimes()
    {
        var items = new[]
        {
            Pkg("Platform", PackageSourceKind.Flatpak, isRuntime: true),
            Pkg("Platform", PackageSourceKind.AppImage),
        };

        PackageDuplicateDetector.Annotate(items);

        Assert.False(items[0].IsDuplicate);
        Assert.False(items[1].IsDuplicate);
    }

    [Fact]
    public void Annotate_ResetsPreviousMarkings()
    {
        var items = new[]
        {
            Pkg("Brave", PackageSourceKind.Flatpak),
            Pkg("Brave", PackageSourceKind.AppImage),
        };
        PackageDuplicateDetector.Annotate(items);
        Assert.True(items[0].IsDuplicate);

        // Eine der Quellen verschwindet -> kein Duplikat mehr.
        var smaller = new[] { items[0] };
        PackageDuplicateDetector.Annotate(smaller);

        Assert.False(items[0].IsDuplicate);
        Assert.Equal("", items[0].DuplicateInfo);
    }

    [Fact]
    public void Annotate_ListsAllOtherSources_WhenThreeWaySplit()
    {
        var items = new[]
        {
            Pkg("Foo", PackageSourceKind.Flatpak),
            Pkg("Foo", PackageSourceKind.AppImage),
            Pkg("Foo", PackageSourceKind.Homebrew),
        };

        PackageDuplicateDetector.Annotate(items);

        // Sortiert nach SourceLabel (OrdinalIgnoreCase): AppImage, Homebrew
        Assert.Equal("AppImage, Homebrew", items[0].DuplicateInfo);
        Assert.Equal("Flatpak, Homebrew", items[1].DuplicateInfo);
        Assert.Equal("AppImage, Flatpak", items[2].DuplicateInfo);
    }

    [Theory]
    [InlineData("Brave Browser", "bravebrowser")]
    [InlineData("OBS-Studio", "obsstudio")]
    [InlineData("  Discord  ", "discord")]
    [InlineData("VSCodium 1.0", "vscodium10")]
    public void Normalize_StripsNonAlnumAndLowercases(string input, string expected)
        => Assert.Equal(expected, PackageDuplicateDetector.Normalize(input));
}
