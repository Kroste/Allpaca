using System.Text;

namespace Allpaca.ViewModels;

/// <summary>
/// Markiert Einträge, die unter normalisiertem Namen in mehreren Quellen auftauchen
/// (klassischer Fall: dieselbe App als Flatpak UND als AppImage). Runtimes werden
/// bewusst ausgenommen, weil sie technische Dependencies sind und sonst nur Larm
/// erzeugen würden.
///
/// Heuristik für v1 absichtlich konservativ: exakter Match auf "lowercase + nur
/// Buchstaben/Ziffern". Damit treffen wir "Brave"/"Brave"; "Brave Browser"/"Brave"
/// würden wir verfehlen - false negatives sind okay, false positives nicht.
/// </summary>
internal static class PackageDuplicateDetector
{
    public static void Annotate(IEnumerable<PackageItemViewModel> items)
    {
        var list = items as ICollection<PackageItemViewModel> ?? items.ToList();

        foreach (var p in list)
        {
            p.IsDuplicate = false;
            p.DuplicateInfo = "";
        }

        var groups = list
            .Where(p => !p.Model.IsRuntime)
            .Where(p => p.Name.Length > 0)
            .GroupBy(p => Normalize(p.Name))
            .Where(g => g.Key.Length > 0)
            .Where(g => g.Select(p => p.Model.Source).Distinct().Count() >= 2);

        foreach (var g in groups)
        {
            var members = g.ToList();
            foreach (var item in members)
            {
                item.IsDuplicate = true;
                item.DuplicateInfo = string.Join(", ", members
                    .Where(o => !ReferenceEquals(o, item))
                    .Select(o => o.SourceLabel)
                    .Distinct()
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            }
        }
    }

    internal static string Normalize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
