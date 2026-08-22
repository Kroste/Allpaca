namespace Allpaca.Services.Ai;

/// <summary>
/// Provider-neutraler KI-Zugriff. Bewusst additiv: fällt die KI aus, läuft
/// Allpaca normal weiter. Streaming ist die primäre Form (chunks fließen live);
/// CompleteAsync ist nur noch ein Convenience-Wrapper, der die Chunks akkumuliert.
/// </summary>
public interface IAiAssistant
{
    AiProvider Provider { get; }
    bool IsConfigured { get; }

    /// <summary>Streamt die KI-Antwort Stück für Stück (deltas, keine kompletten
    /// Sätze). Caller akkumuliert nach Bedarf, kann aber auch live im UI anzeigen.</summary>
    IAsyncEnumerable<string> CompleteStreamAsync(string system, string user, CancellationToken ct = default);

    /// <summary>Convenience: sammelt den Stream und liefert die komplette Antwort.</summary>
    Task<string> CompleteAsync(string system, string user, CancellationToken ct = default);
}
