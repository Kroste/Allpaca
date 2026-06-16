namespace Allpaca.Services.Ai;

/// <summary>Unterstuetzte KI-Backends. Ollama ist der datenschutzfreundliche Default.</summary>
public enum AiProvider
{
    Ollama,     // lokal, OpenAI-kompatibel
    OpenAi,     // ChatGPT
    Anthropic,  // Claude
    Gemini      // Google
}
