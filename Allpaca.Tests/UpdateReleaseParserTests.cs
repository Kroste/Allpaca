using Allpaca.Services;
using Xunit;

namespace Allpaca.Tests;

public class UpdateReleaseParserTests
{
    private const string Json = """
    {
      "tag_name": "v1.7.0",
      "html_url": "https://github.com/Kroste/Allpaca/releases/tag/v1.7.0",
      "assets": [
        { "name": "Allpaca-1.7.0-linux-x64.tar.gz",
          "browser_download_url": "https://example.invalid/tar", "size": 1234 },
        { "name": "Allpaca-1.7.0-x86_64.AppImage",
          "browser_download_url": "https://example.invalid/appimage", "size": 5678 }
      ]
    }
    """;

    [Fact]
    public void Parse_liest_Tag_und_Assets()
    {
        var r = UpdateReleaseParser.Parse(Json);

        Assert.NotNull(r);
        Assert.Equal("v1.7.0", r!.TagName);
        Assert.Equal("1.7.0", r.Version);
        Assert.Equal(2, r.Assets.Count);
        Assert.Equal(5678, r.Assets[1].Size);
    }

    [Fact]
    public void Parse_gibt_null_bei_Muell_statt_zu_werfen()
    {
        Assert.Null(UpdateReleaseParser.Parse("kein json"));
        Assert.Null(UpdateReleaseParser.Parse("{}"));           // kein tag_name
        Assert.Null(UpdateReleaseParser.Parse("[]"));           // falscher Root-Typ
    }

    [Fact]
    public void Parse_ueberspringt_Assets_ohne_Name_oder_Url()
    {
        const string json = """
        { "tag_name": "v1.0.0", "assets": [
            { "name": "ohne-url.tar.gz" },
            { "browser_download_url": "https://example.invalid/x" },
            { "name": "gut.tar.gz", "browser_download_url": "https://example.invalid/gut" } ] }
        """;

        var r = UpdateReleaseParser.Parse(json);

        Assert.NotNull(r);
        Assert.Single(r!.Assets);
        Assert.Equal("gut.tar.gz", r.Assets[0].Name);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("1.2.3+abc123", "1.2.3")]
    public void NormalizeVersion_entfernt_Praefix_und_Metadaten(string raw, string expected)
        => Assert.Equal(expected, UpdateReleaseParser.NormalizeVersion(raw));

    [Theory]
    [InlineData("1.5.1", "1.6.0", true)]
    [InlineData("1.5.1", "v1.5.2", true)]
    // Der Klassiker, an dem ein Stringvergleich scheitert: "1.10.0" < "1.9.0" als Text.
    [InlineData("1.9.0", "1.10.0", true)]
    [InlineData("1.6.0", "1.6.0", false)]
    [InlineData("1.6.0", "1.5.9", false)]
    [InlineData("1.6.0+deadbeef", "1.6.0", false)]
    public void IsNewer_vergleicht_semantisch(string current, string candidate, bool expected)
        => Assert.Equal(expected, UpdateReleaseParser.IsNewer(current, candidate));

    [Fact]
    public void IsNewer_ist_false_bei_unparsbaren_Versionen()
    {
        Assert.False(UpdateReleaseParser.IsNewer("keine-version", "1.0.0"));
        Assert.False(UpdateReleaseParser.IsNewer("1.0.0", "nightly"));
    }

    [Fact]
    public void SelectAsset_waehlt_AppImage_wenn_als_AppImage_gestartet()
    {
        var r = UpdateReleaseParser.Parse(Json)!;

        var asset = UpdateReleaseParser.SelectAsset(r.Assets, runningAsAppImage: true);

        Assert.NotNull(asset);
        Assert.EndsWith(".AppImage", asset!.Name);
    }

    [Fact]
    public void SelectAsset_waehlt_Tarball_bei_normaler_Installation()
    {
        var r = UpdateReleaseParser.Parse(Json)!;

        var asset = UpdateReleaseParser.SelectAsset(r.Assets, runningAsAppImage: false);

        Assert.NotNull(asset);
        Assert.EndsWith("linux-x64.tar.gz", asset!.Name);
    }

    [Fact]
    public void SelectAsset_gibt_null_wenn_die_Installationsform_nicht_bedient_wird()
    {
        var onlyTarball = new[] { new UpdateAsset("Allpaca-1.7.0-linux-x64.tar.gz", "https://example.invalid/t", 1) };

        Assert.Null(UpdateReleaseParser.SelectAsset(onlyTarball, runningAsAppImage: true));
        Assert.Null(UpdateReleaseParser.SelectAsset([], runningAsAppImage: false));
    }
}
