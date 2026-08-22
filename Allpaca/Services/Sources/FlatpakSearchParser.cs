using Allpaca.Models;

namespace Allpaca.Services.Sources;

/// <summary>
/// Parser für "flatpak search --columns=application,name,description,branch,remotes".
/// Tab-getrennte Spalten, eine Zeile pro Treffer. Ältere flatpak-Versionen drucken
/// eine Header-Zeile ("Application ID\tName..."), die filtern wir defensiv mit aus.
/// </summary>
internal static class FlatpakSearchParser
{
    public static IReadOnlyList<PackageInfo> Parse(string stdout)
    {
        var list = new List<PackageInfo>();
        if (string.IsNullOrWhiteSpace(stdout)) return list;

        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;

            // "No matches found" (oder Variante) -> rausfiltern.
            if (line.StartsWith("No matches", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = line.Split('\t');
            var appId = parts[0].Trim();
            if (appId.Length == 0) continue;

            // Header-Zeile älterer flatpak-Versionen abfangen.
            if (appId.Equals("Application ID", StringComparison.OrdinalIgnoreCase)) continue;
            if (appId.Equals("Application", StringComparison.OrdinalIgnoreCase)) continue;

            var name = parts.Length > 1 ? parts[1].Trim() : "";
            var desc = parts.Length > 2 ? parts[2].Trim() : "";
            var origin = parts.Length > 4 ? parts[4].Trim() : "";

            list.Add(new PackageInfo
            {
                Id = appId,
                Name = name.Length > 0 ? name : appId,
                Source = PackageSourceKind.Flatpak,
                Description = desc.Length > 0 ? desc : null,
                Origin = origin.Length > 0 ? origin : null,
            });
        }

        return list;
    }
}
