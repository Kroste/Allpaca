namespace Allpaca.Models;

/// <summary>
/// Quellenneutrale Beschreibung eines installierten Eintrags.
/// "Paket" ist hier bewusst weit gefasst: Flatpak-App, brew-Formula/Cask,
/// rpm-ostree-Layer, Distrobox-Container oder AppImage.
/// </summary>
public sealed record PackageInfo
{
    /// <summary>Technische ID: app-id / formula-token / pkgname / container-name / dateipfad.</summary>
    public required string Id { get; init; }

    /// <summary>Anzeigename.</summary>
    public required string Name { get; init; }

    public string? Version { get; init; }

    public required PackageSourceKind Source { get; init; }

    public string? Description { get; init; }

    public long? SizeBytes { get; init; }

    /// <summary>Remote / Tap / Image / Pfad – je nach Quelle.</summary>
    public string? Origin { get; init; }

    /// <summary>user / system / container / formula / cask / integriert ...</summary>
    public string? Scope { get; init; }

    /// <summary>Flatpak-Runtime statt App.</summary>
    public bool IsRuntime { get; init; }

    /// <summary>Absoluter Dateipfad zu einem PNG-Icon (oder null, wenn die Quelle
    /// keins liefert). Wird vom PackageItemViewModel lazy in Bitmap konvertiert.</summary>
    public string? IconPath { get; init; }

    /// <summary>Quellspezifische Zusatzfelder.</summary>
    public IReadOnlyDictionary<string, string>? Extra { get; init; }
}
