namespace Allpaca.Services;

/// <summary>
/// Sucht Icons im freedesktop-hicolor-Standardpfad. Probiert die ueblichen
/// Groessen in absteigender Schaerfe, gibt den ersten Treffer zurueck. Wir
/// nehmen ausschliesslich PNG - SVG-Rendering braucht ein extra-Paket
/// (Avalonia.Svg.Skia), das fuer 95 % der echten Icons nicht noetig ist.
/// </summary>
public static class IconLookup
{
    // Reihenfolge nach Lese-Praeferenz: erst groessere PNGs, sodass nicht ein 16x16er
    // gewinnt, wenn auch ein 128x128er bereitliegt.
    private static readonly string[] Sizes =
    {
        "128x128", "96x96", "64x64", "256x256", "48x48", "32x32",
    };

    private static readonly string[] BaseDirs = BuildBaseDirs();

    private static string[] BuildBaseDirs()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            // User-installierte Flatpaks: ~/.local/share/flatpak/exports/share/icons/hicolor
            Path.Combine(home, ".local/share/flatpak/exports/share/icons/hicolor"),
            // System-Flatpaks: /var/lib/flatpak/exports/share/icons/hicolor
            "/var/lib/flatpak/exports/share/icons/hicolor",
            // freedesktop Standard (App-Themes, kuratiert, rpm-Layer-Apps)
            Path.Combine(home, ".local/share/icons/hicolor"),
            "/usr/share/icons/hicolor",
            "/usr/local/share/icons/hicolor",
        };
    }

    /// <summary>Sucht ein PNG-Icon zu einer App/Theme-Name. Liefert den absoluten
    /// Pfad oder null, wenn nichts gefunden.</summary>
    public static string? FindPng(string nameOrAppId)
    {
        if (string.IsNullOrWhiteSpace(nameOrAppId)) return null;

        foreach (var baseDir in BaseDirs)
        {
            foreach (var size in Sizes)
            {
                var path = Path.Combine(baseDir, size, "apps", nameOrAppId + ".png");
                if (File.Exists(path)) return path;
            }
        }
        return null;
    }
}
