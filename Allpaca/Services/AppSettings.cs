namespace Allpaca.Services;

/// <summary>
/// Persistierter Nutzer-Zustand. Wird unter $XDG_CONFIG_HOME/Allpaca/settings.json
/// abgelegt - NICHT neben die Exe. Felder hier sind absichtlich konservativ:
/// Theme, Ollama-Endpoint &amp; Co. kommen, sobald sie tatsächlich UI haben.
/// </summary>
public sealed class AppSettings
{
    public string SortKey { get; set; } = "Name";
    public bool SortDescending { get; set; }
    public bool ShowRuntimes { get; set; }

    /// <summary>"Nur aktualisierbare anzeigen"-Toggle aus der OPTIONEN-Sektion.</summary>
    public bool ShowUpdatesOnly { get; set; }

    /// <summary>Pro Quelle (Kind als String): true = im Filter aktiv (Default).</summary>
    public Dictionary<string, bool> SourceFilters { get; set; } = new();

    // --- KI-Konfiguration (v3) ---
    // ApiKey wird BEWUSST nicht hier abgelegt - CLAUDE.md verlangt libsecret/DPAPI.
    // Bis die Secret-Store-Anbindung steht, lebt der Key nur im Speicher (re-enter
    // pro Sitzung). Provider/Endpoint/Modell sind unkritisch und dürfen persistiert.
    public string AiProvider { get; set; } = "Ollama";
    public string? AiEndpoint { get; set; }
    public string? AiModel { get; set; }

    /// <summary>Auto-Refresh-Intervall in Minuten. 0 = aus (Default).</summary>
    public int AutoRefreshMinutes { get; set; }
}
