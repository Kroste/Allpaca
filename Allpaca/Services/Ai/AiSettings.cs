namespace Allpaca.Services.Ai;

/// <summary>
/// Konfiguration des KI-Assistenten. Endpoint/Model duerfen null sein – dann
/// greifen die providerspezifischen Defaults (siehe AiDefaults).
/// </summary>
public sealed record AiSettings
{
    public AiProvider Provider { get; init; } = AiProvider.Ollama;
    public string? Endpoint { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }

    public string ResolvedEndpoint => Endpoint ?? AiDefaults.Endpoint(Provider);
    public string ResolvedModel => Model ?? AiDefaults.Model(Provider);
}

public static class AiDefaults
{
    public static string Endpoint(AiProvider p) => p switch
    {
        AiProvider.Ollama => "http://localhost:11434/v1",
        AiProvider.OpenAi => "https://api.openai.com/v1",
        AiProvider.Anthropic => "https://api.anthropic.com/v1",
        AiProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta",
        _ => "http://localhost:11434/v1",
    };

    public static string Model(AiProvider p) => p switch
    {
        AiProvider.Ollama => "qwen2.5-coder:14b",
        AiProvider.OpenAi => "gpt-4o-mini",
        AiProvider.Anthropic => "claude-sonnet-4-6",
        AiProvider.Gemini => "gemini-2.0-flash",
        _ => "qwen2.5-coder:14b",
    };
}
