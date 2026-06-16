namespace Allpaca.Services;

/// <summary>
/// Persistierter Nutzer-Zustand. Wird unter $XDG_CONFIG_HOME/Allpaca/settings.json
/// abgelegt - NICHT neben die Exe. Felder hier sind absichtlich konservativ:
/// Theme, Ollama-Endpoint &amp; Co. kommen, sobald sie tatsaechlich UI haben.
/// </summary>
public sealed class AppSettings
{
    public string SortKey { get; set; } = "Name";
    public bool SortDescending { get; set; }
    public bool ShowRuntimes { get; set; }

    /// <summary>Pro Quelle (Kind als String): true = im Filter aktiv (Default).</summary>
    public Dictionary<string, bool> SourceFilters { get; set; } = new();
}
