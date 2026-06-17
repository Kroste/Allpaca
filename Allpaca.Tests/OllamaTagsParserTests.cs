using Allpaca.Services.Ai;
using Xunit;

namespace Allpaca.Tests;

public class OllamaTagsParserTests
{
    [Fact]
    public void ParseNames_ExtractsAllModelNames()
    {
        var json = """
        {
          "models": [
            { "name": "qwen2.5-coder:14b", "size": 9000000000 },
            { "name": "llama3.2:3b" },
            { "name": "gemma2:9b", "modified_at": "2025-01-01T00:00:00Z" }
          ]
        }
        """;

        var result = OllamaTagsParser.ParseNames(json);

        Assert.Equal(3, result.Count);
        Assert.Equal("qwen2.5-coder:14b", result[0]);
        Assert.Equal("llama3.2:3b", result[1]);
        Assert.Equal("gemma2:9b", result[2]);
    }

    [Fact]
    public void ParseNames_HandlesEmptyOrMissing()
    {
        Assert.Empty(OllamaTagsParser.ParseNames(""));
        Assert.Empty(OllamaTagsParser.ParseNames("""{"models":[]}"""));
        Assert.Empty(OllamaTagsParser.ParseNames("""{}"""));
    }

    [Fact]
    public void ParseNames_SkipsEntriesWithoutName()
    {
        var json = """
        {
          "models": [
            { "size": 12345 },
            { "name": "", "size": 0 },
            { "name": "valid:tag" }
          ]
        }
        """;

        var result = OllamaTagsParser.ParseNames(json);
        Assert.Single(result);
        Assert.Equal("valid:tag", result[0]);
    }

    [Theory]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434")]
    [InlineData("http://localhost:11434/v1/", "http://localhost:11434")]
    [InlineData("http://localhost:11434", "http://localhost:11434")]
    [InlineData("http://localhost:11434/", "http://localhost:11434")]
    [InlineData("https://ollama.example.com:443/v1", "https://ollama.example.com:443")]
    public void NormalizeApiBase_StripsV1AndTrailingSlash(string input, string expected)
    {
        Assert.Equal(expected, OllamaModelService.NormalizeApiBase(input));
    }
}
