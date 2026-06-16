using System.Text;

namespace Allpaca.Services.Sources;

/// <summary>
/// Erkennt Homebrews "Refusing to load ... from untrusted tap &lt;name&gt;"-Fehler in
/// Log-Zeilen und liefert den Tap-Namen zurueck. Genau diesen Tap muss der User
/// mit "brew trust" markieren, bevor Install/Uninstall fuer dessen Casks/Formulae
/// klappt - typisch auf Bazzite mit ublue-os/tap.
/// </summary>
internal static class UntrustedTapDetector
{
    private const string Marker = "from untrusted tap ";

    public static string? Extract(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        var idx = line.IndexOf(Marker, StringComparison.Ordinal);
        if (idx < 0) return null;

        var rest = line.AsSpan(idx + Marker.Length);
        var sb = new StringBuilder();
        foreach (var ch in rest)
        {
            // Tap-Name endet beim ersten Punkt/Whitespace - brew haengt am Satzende
            // ueblicherweise "." an.
            if (ch == '.' || char.IsWhiteSpace(ch)) break;
            sb.Append(ch);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }
}
