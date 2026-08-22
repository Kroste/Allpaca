namespace Allpaca.Services;

/// <summary>
/// Sucht Icons im freedesktop-hicolor-Standardpfad. Probiert die üblichen
/// PNG-Größen in absteigender Schärfe, fällt am Ende auf das scalable/
/// SVG zurück (das wird zur Render-Zeit von Svg.Skia rastert).
/// </summary>
public static class IconLookup
{
    // Reihenfolge nach Lese-Präferenz: erst größere PNGs, sodass nicht ein 16x16er
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
            // Sandbox-Cache: in der Distrobox/Allpaca-Sandbox ist /var/lib/flatpak NICHT
            // sichtbar - deshalb mirrored FlatpakSource.EnsureIconCacheAsync den
            // System-hicolor-Baum einmalig hierher (~/.cache liegt im geteilten Home).
            Path.Combine(home, ".cache/Allpaca/flatpak-system-icons/hicolor"),
            // System-Flatpaks direkt vom Host (greift, wenn Allpaca nativ läuft)
            "/var/lib/flatpak/exports/share/icons/hicolor",
            // freedesktop Standard (App-Themes, kuratiert, rpm-Layer-Apps)
            Path.Combine(home, ".local/share/icons/hicolor"),
            "/usr/share/icons/hicolor",
            "/usr/local/share/icons/hicolor",
        };
    }

    /// <summary>Sucht ein Icon zu einer App/Theme-Name. Liefert den absoluten Pfad
    /// (PNG bevorzugt, SVG als Fallback) oder null, wenn nichts gefunden. Pro
    /// Basis-Verzeichnis erst alle PNG-Größen, dann das scalable/SVG - sodass
    /// ein lokal vorhandenes PNG immer Vorrang vor einem irgendwo systemweiten SVG
    /// hat.</summary>
    public static string? FindIcon(string nameOrAppId)
    {
        if (string.IsNullOrWhiteSpace(nameOrAppId)) return null;

        foreach (var baseDir in BaseDirs)
        {
            foreach (var size in Sizes)
            {
                var path = Path.Combine(baseDir, size, "apps", nameOrAppId + ".png");
                if (File.Exists(path)) return path;
            }
            var svg = Path.Combine(baseDir, "scalable", "apps", nameOrAppId + ".svg");
            if (File.Exists(svg)) return svg;
        }
        return null;
    }

    /// <summary>Alter Name, behalten für Backward-Compat im Test-Projekt.</summary>
    [System.Obsolete("Verwende FindIcon - es findet jetzt auch SVGs.")]
    public static string? FindPng(string nameOrAppId) => FindIcon(nameOrAppId);
}
