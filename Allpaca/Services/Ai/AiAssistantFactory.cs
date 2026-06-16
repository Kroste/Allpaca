using System.Net.Http;

namespace Allpaca.Services.Ai;

/// <summary>Erzeugt den passenden Assistenten zur Konfiguration.</summary>
public static class AiAssistantFactory
{
    public static IAiAssistant Create(AiSettings settings, HttpClient? http = null)
    {
        var client = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        return settings.Provider switch
        {
            // Ollama und OpenAI sprechen beide das OpenAI-Chat-Completions-Format.
            AiProvider.Ollama or AiProvider.OpenAi => new OpenAiCompatibleAssistant(settings, client),
            AiProvider.Anthropic => new AnthropicAssistant(settings, client),
            AiProvider.Gemini => new GeminiAssistant(settings, client),
            _ => new OpenAiCompatibleAssistant(settings with { Provider = AiProvider.Ollama }, client),
        };
    }
}
