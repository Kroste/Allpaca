namespace Allpaca.Services.Sources;

/// <summary>
/// Parser für die textuelle Ausgabe von "rpm-ostree upgrade --check".
/// Beispiel bei verfügbarem Update:
///
///   AvailableUpdate:
///     Version: 41.20241115.0 (2024-11-15T00:55:09Z)
///     Commit: a1b2c3...
///
/// Ohne Update kommt Exit-Code 77 (vom RpmOstreeSource ausgewertet) und meistens
/// ein knapper "No updates available."-Text - den kommen wir hier nicht zu sehen.
/// </summary>
internal static class RpmOstreeUpgradeCheckParser
{
    /// <summary>
    /// Extrahiert die "Version: ..."-Zeile aus dem AvailableUpdate-Block.
    /// Gibt null zurück, wenn keine Version-Zeile gefunden wurde.
    /// </summary>
    public static string? ExtractAvailableVersion(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout)) return null;

        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            // Format: "Version: <wert> (timestamp)"
            const string prefix = "Version:";
            if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var rest = line.Substring(prefix.Length).Trim();
            if (rest.Length == 0) continue;

            // Zeitstempel-Klammer abschneiden, falls vorhanden.
            var paren = rest.IndexOf('(');
            if (paren > 0) rest = rest.Substring(0, paren).Trim();

            return rest.Length > 0 ? rest : null;
        }
        return null;
    }
}
