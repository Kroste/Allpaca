namespace Allpaca.Services.Ai;

/// <summary>
/// Provider-neutraler KI-Zugriff. Bewusst additiv: faellt die KI aus, laeuft
/// Allpaca normal weiter. Streaming ist die primaere Form (chunks fliessen live);
/// CompleteAsync ist nur noch ein Convenience-Wrapper, der die Chunks akkumuliert.
/// </summary>
public interface IAiAssistant
{
    AiProvider Provider { get; }
    bool IsConfigured { get; }

    /// <summary>Streamt die KI-Antwort Stueck fuer Stueck (deltas, keine kompletten
    /// Saetze). Caller akkumuliert nach Bedarf, kann aber auch live im UI anzeigen.</summary>
    IAsyncEnumerable<string> CompleteStreamAsync(string system, string user, CancellationToken ct = default);

    /// <summary>Convenience: sammelt den Stream und liefert die komplette Antwort.</summary>
    Task<string> CompleteAsync(string system, string user, CancellationToken ct = default);
}
