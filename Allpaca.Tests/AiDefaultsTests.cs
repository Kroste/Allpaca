using Allpaca.Services.Ai;
using Xunit;

namespace Allpaca.Tests;

public class AiDefaultsTests
{
    [Fact]
    public void Ollama_IsLocalDefault()
    {
        var s = new AiSettings();   // Default = Ollama
        Assert.Equal(AiProvider.Ollama, s.Provider);
        Assert.StartsWith("http://localhost:11434", s.ResolvedEndpoint);
    }

    [Theory]
    [InlineData(AiProvider.OpenAi)]
    [InlineData(AiProvider.Anthropic)]
    [InlineData(AiProvider.Gemini)]
    public void EveryProvider_HasEndpointAndModel(AiProvider provider)
    {
        var s = new AiSettings { Provider = provider };
        Assert.False(string.IsNullOrWhiteSpace(s.ResolvedEndpoint));
        Assert.False(string.IsNullOrWhiteSpace(s.ResolvedModel));
    }
}
