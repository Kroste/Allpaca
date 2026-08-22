namespace Allpaca.Services.Ai;

/// <summary>
/// Eine kleine kuratierte Liste für Allpaca-typische Aufgaben (Empfehlung, Aufräum-
/// Analyse, Fehler-Diagnose). Bewusst klein gehalten - der User kann jeden anderen
/// Ollama-Namen auch direkt eintippen.
/// </summary>
public sealed record OllamaCuratedModel(string Name, string ApproxSize, string Description);

public static class OllamaCuratedModels
{
    public static IReadOnlyList<OllamaCuratedModel> All { get; } = new[]
    {
        new OllamaCuratedModel("qwen2.5-coder:7b",  "~4.7 GB", "Code-fokussiert, klein, schnell"),
        new OllamaCuratedModel("qwen2.5-coder:14b", "~9.0 GB", "Code-fokussiert, größer, Default in AiDefaults"),
        new OllamaCuratedModel("llama3.2:3b",       "~2.0 GB", "Allgemein, sehr klein, gut für Tests"),
        new OllamaCuratedModel("gemma2:9b",         "~5.5 GB", "Allgemein, ausgewogen"),
        new OllamaCuratedModel("mistral:7b",        "~4.1 GB", "Allgemein, beliebter Klassiker"),
    };
}
