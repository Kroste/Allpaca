using Allpaca.Models;
using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class FlatpakSearchParserTests
{
    [Fact]
    public void Parse_HandlesTabSeparatedRows()
    {
        var input =
            "org.mozilla.firefox\tFirefox\tWebbrowser\tstable\tflathub\n" +
            "com.brave.Browser\tBrave Browser\tBrowse the web safely\tstable\tflathub\n";

        var result = FlatpakSearchParser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("org.mozilla.firefox", result[0].Id);
        Assert.Equal("Firefox", result[0].Name);
        Assert.Equal("Webbrowser", result[0].Description);
        Assert.Equal("flathub", result[0].Origin);
        Assert.Equal(PackageSourceKind.Flatpak, result[0].Source);
    }

    [Fact]
    public void Parse_SkipsHeaderRow()
    {
        var input =
            "Application ID\tName\tDescription\tBranch\tRemotes\n" +
            "org.gnu.emacs\tEmacs\tText editor\tstable\tflathub\n";

        var result = FlatpakSearchParser.Parse(input);

        Assert.Single(result);
        Assert.Equal("org.gnu.emacs", result[0].Id);
    }

    [Fact]
    public void Parse_SkipsNoMatchesLine()
    {
        var result = FlatpakSearchParser.Parse("No matches found");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_FallsBackNameToId_WhenNameMissing()
    {
        var input = "com.example.App\t\t\tstable\tflathub";
        var result = FlatpakSearchParser.Parse(input);

        Assert.Single(result);
        Assert.Equal("com.example.App", result[0].Id);
        Assert.Equal("com.example.App", result[0].Name);
        Assert.Null(result[0].Description);
    }

    [Fact]
    public void Parse_HandlesEmptyInput()
    {
        Assert.Empty(FlatpakSearchParser.Parse(""));
        Assert.Empty(FlatpakSearchParser.Parse("   "));
    }
}
