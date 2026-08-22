using Allpaca.Models;

namespace Allpaca.Services.Ai;

/// <summary>
/// Parser für die KI-Antwort von AiSuggestionPromptBuilder. Erwartet Zeilen im
/// Format "PROVIDER|PAKET-ID|BEGRÜNDUNG". Tolerant gegen Vor-/Nachgeplapper und
/// Provider-Schreibvarianten (flatpak, Flatpak, brew, Homebrew); Zeilen, die nicht
/// passen, werden schlicht ignoriert.
/// </summary>
internal static class AiSuggestionParser
{
    public static IReadOnlyList<PackageInfo> Parse(string? response)
    {
        var list = new List<PackageInfo>();
        if (string.IsNullOrWhiteSpace(response)) return list;

        foreach (var raw in response.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;

            var parts = line.Split('|');
            if (parts.Length < 2) continue;

            var providerToken = parts[0].Trim();
            var id = parts[1].Trim();
            if (id.Length == 0) continue;

            var providerLower = providerToken.ToLowerInvariant();
            PackageSourceKind kind;
            if (providerLower.Contains("flatpak")) kind = PackageSourceKind.Flatpak;
            else if (providerLower.Contains("brew") || providerLower.Contains("homebrew")) kind = PackageSourceKind.Homebrew;
            else continue;

            var reason = parts.Length > 2 ? parts[2].Trim() : "";

            list.Add(new PackageInfo
            {
                Id = id,
                Name = id,
                Source = kind,
                // 🤖-Prefix macht die KI-Herkunft im SearchWindow-Result-Template sichtbar.
                Description = reason.Length > 0 ? "🤖 " + reason : "🤖 KI-Empfehlung",
            });
        }
        return list;
    }
}
