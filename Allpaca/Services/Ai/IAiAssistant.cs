namespace Allpaca.Services.Ai;

/// <summary>
/// Provider-neutraler KI-Zugriff. Bewusst additiv: faellt die KI aus, laeuft
/// Allpaca normal weiter. (Streaming folgt in v3; v1/v2 nutzen Single-Shot.)
/// </summary>
public interface IAiAssistant
{
    AiProvider Provider { get; }
    bool IsConfigured { get; }

    Task<string> CompleteAsync(string system, string user, CancellationToken ct = default);
}
