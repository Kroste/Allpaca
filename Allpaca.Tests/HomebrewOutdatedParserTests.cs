using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class HomebrewOutdatedParserTests
{
    [Fact]
    public void ParseIds_PicksUpFormulae()
    {
        var json = """
        {
          "formulae": [
            { "name": "node", "installed_versions": ["20.0.0"], "current_version": "21.5.0" },
            { "name": "git",  "installed_versions": ["2.40.0"], "current_version": "2.43.0" }
          ],
          "casks": []
        }
        """;

        var result = HomebrewOutdatedParser.ParseIds(json);

        Assert.Equal(2, result.Count);
        Assert.Contains("node", result);
        Assert.Contains("git", result);
    }

    [Fact]
    public void ParseIds_PrefersCaskToken_OverName()
    {
        // brew outdated --json=v2 fuer Casks: token = ID, name = Anzeigename.
        // Wir wollen den token, weil ListInstalledAsync ihn als Id speichert.
        var json = """
        {
          "formulae": [],
          "casks": [
            { "name": "Visual Studio Code", "token": "visual-studio-code",
              "installed_versions": "1.85.0", "current_version": "1.90.0" }
          ]
        }
        """;

        var result = HomebrewOutdatedParser.ParseIds(json);

        Assert.Single(result);
        Assert.Contains("visual-studio-code", result);
    }

    [Fact]
    public void ParseIds_HandlesEmptyOrMissing()
    {
        Assert.Empty(HomebrewOutdatedParser.ParseIds(""));
        Assert.Empty(HomebrewOutdatedParser.ParseIds("""{"formulae":[],"casks":[]}"""));
        Assert.Empty(HomebrewOutdatedParser.ParseIds("""{}"""));
    }

    [Fact]
    public void ParseIds_MixedFormulaeAndCasks()
    {
        var json = """
        {
          "formulae": [{ "name": "wget" }],
          "casks":    [{ "token": "brave-browser", "name": "Brave Browser" }]
        }
        """;

        var result = HomebrewOutdatedParser.ParseIds(json);

        Assert.Equal(2, result.Count);
        Assert.Contains("wget", result);
        Assert.Contains("brave-browser", result);
    }

    [Fact]
    public void ParseIds_SkipsEntriesWithoutUsableField()
    {
        var json = """
        {
          "formulae": [{ "current_version": "1.0" }, { "name": "" }, { "name": "valid" }],
          "casks": []
        }
        """;

        var result = HomebrewOutdatedParser.ParseIds(json);

        Assert.Single(result);
        Assert.Contains("valid", result);
    }
}
