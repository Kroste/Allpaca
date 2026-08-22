using System.Text;
using Allpaca.Models;
using Allpaca.ViewModels;

namespace Allpaca.Services.Ai;

/// <summary>
/// Baut den Prompt für die "KI-Aufräum-Analyse" - pur statisch, damit
/// Truncation + Formatierung getestet werden können ohne KI-Mock.
/// </summary>
public static class CleanupPromptBuilder
{
    public const string SystemPrompt =
        "Du bist Linux-Paket-Manager-Experte für Bazzite (Fedora Atomic). " +
        "Du kennst Flatpak, Homebrew, rpm-ostree, Distrobox und AppImage. " +
        "Du bekommst die Liste aller installierten Pakete des Users. " +
        "Antworte auf Deutsch in Klartext (kein Markdown) in genau drei Abschnitten, " +
        "Reihenfolge fest:\n" +
        "1) DUPLIKATE – Apps, die in mehreren Quellen existieren. Empfehle, welche Variante " +
        "zu behalten ist (Flatpak ist auf Bazzite meist die beste Wahl) und nenne, was deinstalliert " +
        "werden kann.\n" +
        "2) WAISEN-VERDACHT – Pakete, die typischerweise als Dependency anderer mitkommen und " +
        "selten direkt vom User installiert werden. Wenn unklar, sag das ehrlich.\n" +
        "3) EVENTUELL ÜBERFLÜSSIG – Tools, die der User vielleicht nicht mehr braucht " +
        "(z. B. Test-/Demo-Apps, alte Versionen). Vorsichtig, nichts erfinden.\n" +
        "Max 30 Zeilen total. Pro Eintrag eine knappe Begründung. Lieber wenige sichere Empfehlungen " +
        "als viele schwache.";

    /// <summary>Pro Quelle so viele Einträge - bei größeren Listen wird der Rest gekappt.</summary>
    public const int MaxPerSource = 200;

    public static string BuildUserPrompt(IReadOnlyList<PackageItemViewModel> all)
    {
        var sb = new StringBuilder();

        // Runtimes sind technische Dependencies, kein Cleanup-Material.
        var groups = all
            .Where(p => !p.Model.IsRuntime)
            .GroupBy(p => p.Model.Source)
            .OrderBy(g => g.Key.ToString(), StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            var items = g.ToList();
            sb.Append(SourceLabel(g.Key)).Append(" (").Append(items.Count).AppendLine("):");

            var skipped = Math.Max(0, items.Count - MaxPerSource);
            foreach (var p in items.Take(MaxPerSource))
            {
                sb.Append("- ").Append(p.Name);

                if (!string.IsNullOrWhiteSpace(p.Version))
                    sb.Append(' ').Append(p.Version);
                if (!string.Equals(p.Name, p.Id, StringComparison.Ordinal))
                    sb.Append(" [").Append(p.Id).Append(']');
                if (p.IsDuplicate && !string.IsNullOrWhiteSpace(p.DuplicateInfo))
                    sb.Append(" (auch in: ").Append(p.DuplicateInfo).Append(')');

                sb.AppendLine();
            }
            if (skipped > 0)
                sb.Append("(weitere ").Append(skipped).AppendLine(" Einträge gekürzt)");

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string SourceLabel(PackageSourceKind kind) => kind switch
    {
        PackageSourceKind.Flatpak => "Flatpak",
        PackageSourceKind.Homebrew => "Homebrew",
        PackageSourceKind.RpmOstree => "rpm-ostree (gelayert)",
        PackageSourceKind.Distrobox => "Distrobox-Container",
        PackageSourceKind.AppImage => "AppImage",
        PackageSourceKind.Pipx => "pipx (Python-CLI)",
        _ => kind.ToString(),
    };
}
