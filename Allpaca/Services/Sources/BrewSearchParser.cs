using Allpaca.Models;

namespace Allpaca.Services.Sources;

/// <summary>
/// Parser für "brew search &lt;query&gt;". brew gruppiert die Treffer in zwei
/// Sections über "==&gt; Formulae" und "==&gt; Casks". Wir behalten die Section-Info
/// als Scope ("formula" / "cask"), damit die UI sie als Hinweis zeigen kann.
/// </summary>
internal static class BrewSearchParser
{
    public static IReadOnlyList<PackageInfo> Parse(string stdout)
    {
        var list = new List<PackageInfo>();
        if (string.IsNullOrWhiteSpace(stdout)) return list;

        string section = "";  // "formula" | "cask" | ""

        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;

            // Section-Header. brew schreibt entweder "==> Formulae" oder "==> Casks";
            // gelegentlich auch lokal angepasste Texte. Wir matchen tolerant.
            if (line.StartsWith("==>", StringComparison.Ordinal))
            {
                if (line.Contains("Cask", StringComparison.OrdinalIgnoreCase)) section = "cask";
                else if (line.Contains("Formula", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("Formulae", StringComparison.OrdinalIgnoreCase)) section = "formula";
                else section = "";
                continue;
            }

            // Ohne Section keine Zuordnung -> überspringen (z. B. "No formula found" Zeile).
            if (section.Length == 0) continue;

            // brew search liefert ggf. mehrere Tokens pro Zeile (kommagetrennt in
            // manchen Versionen). Wir nehmen alle.
            foreach (var token in line.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = token.Trim();
                // Annotationen wie "(installed)" rausfiltern.
                if (name.StartsWith("(") || name.EndsWith(":")) continue;
                if (name.Length == 0) continue;

                list.Add(new PackageInfo
                {
                    Id = name,
                    Name = name,
                    Source = PackageSourceKind.Homebrew,
                    Scope = section,
                });
            }
        }

        return list;
    }
}
