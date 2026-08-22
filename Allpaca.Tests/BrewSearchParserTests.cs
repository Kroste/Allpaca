using Allpaca.Models;
using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class BrewSearchParserTests
{
    [Fact]
    public void Parse_GroupsFormulaeAndCasks()
    {
        var input =
            "==> Formulae\n" +
            "firefox-bin\n" +
            "firefox-mac\n" +
            "==> Casks\n" +
            "firefox\n" +
            "firefox-beta\n";

        var result = BrewSearchParser.Parse(input);

        Assert.Equal(4, result.Count);
        Assert.All(result, p => Assert.Equal(PackageSourceKind.Homebrew, p.Source));
        Assert.Equal("formula", result[0].Scope);
        Assert.Equal("formula", result[1].Scope);
        Assert.Equal("cask", result[2].Scope);
        Assert.Equal("cask", result[3].Scope);
    }

    [Fact]
    public void Parse_HandlesMultipleTokensPerLine()
    {
        // Manche brew-Versionen drucken mehrere Namen pro Zeile.
        var input =
            "==> Formulae\n" +
            "foo bar baz\n" +
            "==> Casks\n" +
            "alpha, beta\n";

        var result = BrewSearchParser.Parse(input);

        Assert.Equal(5, result.Count);
        Assert.Contains(result, p => p.Name == "foo" && p.Scope == "formula");
        Assert.Contains(result, p => p.Name == "alpha" && p.Scope == "cask");
        Assert.Contains(result, p => p.Name == "beta" && p.Scope == "cask");
    }

    [Fact]
    public void Parse_IgnoresLinesOutsideOfSection()
    {
        var input =
            "Searching for ...\n" +
            "==> Formulae\n" +
            "wget\n";

        var result = BrewSearchParser.Parse(input);

        Assert.Single(result);
        Assert.Equal("wget", result[0].Name);
    }

    [Fact]
    public void Parse_HandlesEmptyOrUnrelatedInput()
    {
        Assert.Empty(BrewSearchParser.Parse(""));
        Assert.Empty(BrewSearchParser.Parse("No results"));
        Assert.Empty(BrewSearchParser.Parse("==> SomethingElse\nfoo\n"));
    }

    [Fact]
    public void Parse_SkipsAnnotationTokens()
    {
        // brew kann Token in Klammern hinten dranhängen wie "(installed)".
        var input =
            "==> Formulae\n" +
            "node (installed)\n";

        var result = BrewSearchParser.Parse(input);

        Assert.Single(result);
        Assert.Equal("node", result[0].Name);
    }
}
