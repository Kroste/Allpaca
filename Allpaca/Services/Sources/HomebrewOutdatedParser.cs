using System.Text.Json;

namespace Allpaca.Services.Sources;

/// <summary>
/// Parser fuer "brew outdated --json=v2". Struktur: {"formulae":[{name,...}], "casks":[{name,...}]}.
/// Wir interessieren uns nur fuer die Namen/Tokens - das matcht die IDs, die wir in
/// HomebrewSource.ListInstalledAsync vergeben (Formulae: name, Casks: token).
/// </summary>
internal static class HomebrewOutdatedParser
{
    public static IReadOnlySet<string> ParseIds(string json)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return set;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("formulae", out var formulae)
            && formulae.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in formulae.EnumerateArray())
                if (f.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name)
                    set.Add(name);
        }

        if (root.TryGetProperty("casks", out var casks)
            && casks.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in casks.EnumerateArray())
            {
                // Casks haben "name" (Anzeigename) und "token" (ID). Wir verwenden token,
                // weil unsere ListInstalledAsync den Cask unter token als Id speichert.
                if (c.TryGetProperty("token", out var t) && t.GetString() is { Length: > 0 } token)
                    set.Add(token);
                else if (c.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name)
                    set.Add(name);
            }
        }

        return set;
    }
}
