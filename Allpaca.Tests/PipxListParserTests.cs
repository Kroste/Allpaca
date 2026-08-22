using Allpaca.Models;
using Allpaca.Services.Sources;
using Xunit;

namespace Allpaca.Tests;

public class PipxListParserTests
{
    [Fact]
    public void Parse_RealisticOutput()
    {
        // Auszug aus echtem "pipx list --json" auf Bazzite.
        var json = """
        {
          "pipx_spec_version": "0.1",
          "venvs": {
            "yt-dlp": {
              "metadata": {
                "main_package": {
                  "package": "yt-dlp",
                  "package_version": "2024.10.22"
                }
              }
            },
            "httpie": {
              "metadata": {
                "main_package": {
                  "package": "httpie",
                  "package_version": "3.2.4"
                }
              }
            }
          }
        }
        """;

        var result = PipxListParser.Parse(json);

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal(PackageSourceKind.Pipx, p.Source));
        Assert.All(result, p => Assert.Equal("user", p.Scope));
        Assert.Contains(result, p => p.Name == "yt-dlp" && p.Version == "2024.10.22");
        Assert.Contains(result, p => p.Name == "httpie" && p.Version == "3.2.4");
    }

    [Fact]
    public void Parse_HandlesEmptyOrMissingVenvs()
    {
        Assert.Empty(PipxListParser.Parse(""));
        Assert.Empty(PipxListParser.Parse("""{"pipx_spec_version":"0.1","venvs":{}}"""));
        Assert.Empty(PipxListParser.Parse("""{"pipx_spec_version":"0.1"}"""));
    }

    [Fact]
    public void Parse_FallsBackToVenvName_WhenMetadataMissing()
    {
        // Falls eine spätere pipx-Version das Schema verändert: zumindest den
        // venv-Schlüssel als Name verwenden, damit der Eintrag nicht verschluckt wird.
        var json = """
        {
          "venvs": {
            "orphan-tool": { }
          }
        }
        """;

        var result = PipxListParser.Parse(json);

        Assert.Single(result);
        Assert.Equal("orphan-tool", result[0].Name);
        Assert.Equal("orphan-tool", result[0].Id);
        Assert.Null(result[0].Version);
    }

    [Fact]
    public void Parse_HandlesPackageWithoutVersion()
    {
        var json = """
        {
          "venvs": {
            "tool": {
              "metadata": {
                "main_package": { "package": "tool" }
              }
            }
          }
        }
        """;

        var result = PipxListParser.Parse(json);

        Assert.Single(result);
        Assert.Equal("tool", result[0].Name);
        Assert.Null(result[0].Version);
    }
}
