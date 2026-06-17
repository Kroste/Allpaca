using Allpaca.Models;
using Allpaca.Services.Ai;
using Allpaca.ViewModels;
using Xunit;

namespace Allpaca.Tests;

public class CleanupPromptBuilderTests
{
    private static PackageItemViewModel Pkg(
        string name, PackageSourceKind src,
        string? id = null, string? version = null, bool isRuntime = false)
        => new(new PackageInfo
        {
            Id = id ?? name,
            Name = name,
            Source = src,
            Version = version,
            IsRuntime = isRuntime,
        });

    [Fact]
    public void BuildUserPrompt_GroupsBySourceWithCount()
    {
        var pkgs = new[]
        {
            Pkg("Firefox", PackageSourceKind.Flatpak, "org.mozilla.firefox", "135.0"),
            Pkg("Brave", PackageSourceKind.Flatpak, "com.brave.Browser", "1.91"),
            Pkg("htop", PackageSourceKind.Homebrew, version: "3.4.1"),
        };

        var prompt = CleanupPromptBuilder.BuildUserPrompt(pkgs);

        Assert.Contains("Flatpak (2):", prompt);
        Assert.Contains("Homebrew (1):", prompt);
        Assert.Contains("- Firefox 135.0 [org.mozilla.firefox]", prompt);
        Assert.Contains("- htop 3.4.1", prompt);  // Name == Id, kein [] dahinter
    }

    [Fact]
    public void BuildUserPrompt_SkipsRuntimes()
    {
        var pkgs = new[]
        {
            Pkg("Firefox", PackageSourceKind.Flatpak),
            Pkg("org.gnome.Platform", PackageSourceKind.Flatpak, isRuntime: true),
        };

        var prompt = CleanupPromptBuilder.BuildUserPrompt(pkgs);

        Assert.Contains("Flatpak (1):", prompt);
        Assert.Contains("Firefox", prompt);
        Assert.DoesNotContain("org.gnome.Platform", prompt);
    }

    [Fact]
    public void BuildUserPrompt_MarksDuplicatesWithDuplicateInfo()
    {
        var fp = Pkg("Brave", PackageSourceKind.Flatpak, "com.brave.Browser");
        var ai = Pkg("Brave", PackageSourceKind.AppImage, "/home/u/Apps/Brave.AppImage");

        // PackageDuplicateDetector normalerweise via MainWindowViewModel.ApplyFilter -
        // hier setzen wir die Felder direkt fuer den Test.
        PackageDuplicateDetector.Annotate(new[] { fp, ai });

        var prompt = CleanupPromptBuilder.BuildUserPrompt(new[] { fp, ai });

        Assert.Contains("(auch in: AppImage)", prompt);
        Assert.Contains("(auch in: Flatpak)", prompt);
    }

    [Fact]
    public void BuildUserPrompt_TruncatesLongLists()
    {
        var pkgs = new System.Collections.Generic.List<PackageItemViewModel>();
        for (int i = 0; i < CleanupPromptBuilder.MaxPerSource + 50; i++)
            pkgs.Add(Pkg($"pkg-{i}", PackageSourceKind.Homebrew));

        var prompt = CleanupPromptBuilder.BuildUserPrompt(pkgs);

        Assert.Contains("Homebrew (" + (CleanupPromptBuilder.MaxPerSource + 50) + "):", prompt);
        Assert.Contains("(weitere 50 Einträge gekürzt)", prompt);
        Assert.Contains("pkg-0", prompt);
        Assert.DoesNotContain($"pkg-{CleanupPromptBuilder.MaxPerSource + 10}", prompt);
    }

    [Fact]
    public void BuildUserPrompt_HandlesEmpty()
    {
        var prompt = CleanupPromptBuilder.BuildUserPrompt(System.Array.Empty<PackageItemViewModel>());
        Assert.Equal("", prompt);
    }
}
