using Allpaca.Models;
using Allpaca.ViewModels;
using Xunit;

namespace Allpaca.Tests;

public class PackageSorterTests
{
    private static PackageItemViewModel Pkg(string name, PackageSourceKind src, long? size = null)
        => new(new PackageInfo { Id = name, Name = name, Source = src, SizeBytes = size });

    [Fact]
    public void SortByName_Ascending_IsCaseInsensitive()
    {
        var items = new[]
        {
            Pkg("Zed", PackageSourceKind.Flatpak),
            Pkg("alpha", PackageSourceKind.Flatpak),
            Pkg("Bravo", PackageSourceKind.Flatpak),
        };

        var sorted = PackageSorter.Sort(items, SortKey.Name, descending: false).ToList();

        Assert.Equal(new[] { "alpha", "Bravo", "Zed" }, sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortByName_Descending_ReversesOrder()
    {
        var items = new[]
        {
            Pkg("alpha", PackageSourceKind.Flatpak),
            Pkg("Bravo", PackageSourceKind.Flatpak),
            Pkg("Zed", PackageSourceKind.Flatpak),
        };

        var sorted = PackageSorter.Sort(items, SortKey.Name, descending: true).ToList();

        Assert.Equal(new[] { "Zed", "Bravo", "alpha" }, sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortBySize_Descending_PutsLargestFirst_AndUnknownAtEnd()
    {
        var items = new[]
        {
            Pkg("Small", PackageSourceKind.Flatpak, 1_000),
            Pkg("Unknown", PackageSourceKind.Distrobox, null),
            Pkg("Large", PackageSourceKind.Flatpak, 1_000_000),
            Pkg("Medium", PackageSourceKind.Flatpak, 5_000),
        };

        var sorted = PackageSorter.Sort(items, SortKey.Size, descending: true).ToList();

        Assert.Equal(new[] { "Large", "Medium", "Small", "Unknown" }, sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortBySize_Ascending_KeepsUnknownAtEnd()
    {
        var items = new[]
        {
            Pkg("Unknown", PackageSourceKind.Distrobox, null),
            Pkg("Medium", PackageSourceKind.Flatpak, 5_000),
            Pkg("Small", PackageSourceKind.Flatpak, 1_000),
        };

        var sorted = PackageSorter.Sort(items, SortKey.Size, descending: false).ToList();

        Assert.Equal(new[] { "Small", "Medium", "Unknown" }, sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortBySource_Ascending_GroupsBySourceLabel_ThenByName()
    {
        var items = new[]
        {
            Pkg("zeta", PackageSourceKind.Flatpak),
            Pkg("alpha", PackageSourceKind.Homebrew),
            Pkg("beta", PackageSourceKind.Flatpak),
        };

        var sorted = PackageSorter.Sort(items, SortKey.Source, descending: false).ToList();

        // Source-Label-Reihenfolge (asc, OrdinalIgnoreCase): "Flatpak" < "Homebrew"
        Assert.Equal(new[] { "beta", "zeta", "alpha" }, sorted.Select(p => p.Name));
    }
}
