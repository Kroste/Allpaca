using Allpaca.Models;
using Allpaca.Services.Ai;
using Xunit;

namespace Allpaca.Tests;

public class AiSuggestionParserTests
{
    [Fact]
    public void Parse_StrictFormat_AllProviders()
    {
        var response = """
        Flatpak|org.gimp.GIMP|GUI-Bildbearbeitung, etabliert.
        Homebrew|ffmpeg|CLI-Werkzeug für Video-Encoding und -Schneiden.
        Flatpak|org.kde.kdenlive|Komfortabler Videoschnitt mit Timeline.
        """;

        var result = AiSuggestionParser.Parse(response);

        Assert.Equal(3, result.Count);
        Assert.Equal(PackageSourceKind.Flatpak, result[0].Source);
        Assert.Equal("org.gimp.GIMP", result[0].Id);
        Assert.StartsWith("🤖 ", result[0].Description);
        Assert.Equal(PackageSourceKind.Homebrew, result[1].Source);
        Assert.Equal("ffmpeg", result[1].Id);
    }

    [Theory]
    [InlineData("flatpak|org.foo.Bar|x", PackageSourceKind.Flatpak)]
    [InlineData("FLATPAK|org.foo.Bar|x", PackageSourceKind.Flatpak)]
    [InlineData("Homebrew|wget|x", PackageSourceKind.Homebrew)]
    [InlineData("brew|wget|x", PackageSourceKind.Homebrew)]
    [InlineData("homebrew|wget|x", PackageSourceKind.Homebrew)]
    public void Parse_AcceptsProviderVariants(string line, PackageSourceKind expected)
    {
        var result = AiSuggestionParser.Parse(line);
        Assert.Single(result);
        Assert.Equal(expected, result[0].Source);
    }

    [Fact]
    public void Parse_SkipsUnknownProviders()
    {
        var response = "AppImage|whatever|skip me\nFlatpak|valid.id|ok";
        var result = AiSuggestionParser.Parse(response);

        Assert.Single(result);
        Assert.Equal("valid.id", result[0].Id);
    }

    [Fact]
    public void Parse_HandlesMissingReason()
    {
        // Nur Provider|Id, ohne dritte Spalte - sollte trotzdem als Eintrag durchkommen
        // mit Default-Begründungstext.
        var result = AiSuggestionParser.Parse("Flatpak|org.foo");
        Assert.Single(result);
        Assert.Equal("org.foo", result[0].Id);
        Assert.Equal("🤖 KI-Empfehlung", result[0].Description);
    }

    [Fact]
    public void Parse_SkipsEmptyAndGarbage()
    {
        var response = """
        Hier ist meine Antwort:

        Flatpak|org.real.App|gut

        irgendein Text dazwischen
        |||
        Flatpak||leere ID
        Homebrew|valid|ok
        """;

        var result = AiSuggestionParser.Parse(response);

        Assert.Equal(2, result.Count);
        Assert.Equal("org.real.App", result[0].Id);
        Assert.Equal("valid", result[1].Id);
    }

    [Fact]
    public void Parse_HandlesEmptyAndNullResponse()
    {
        Assert.Empty(AiSuggestionParser.Parse(null));
        Assert.Empty(AiSuggestionParser.Parse(""));
        Assert.Empty(AiSuggestionParser.Parse("   "));
    }
}
