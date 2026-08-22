using Allpaca.Models;

namespace Allpaca.Services;

/// <summary>
/// Pro Quelle ein konkreter „so installierst du das Tool"-Hint, der im Sidebar-
/// ToolTip erscheint, wenn die Quelle als nicht verfügbar gemeldet wird.
/// Bewusst Bazzite-zentriert: rpm-ostree für System-Layer, Homebrew als
/// rootless Alternative, ujust-Pfade wo praktisch.
/// </summary>
public static class SourceRecoveryHints
{
    public static string? For(PackageSourceKind kind) => kind switch
    {
        PackageSourceKind.Flatpak =>
            "Flatpak ist auf Bazzite normalerweise ab Werk dabei. Falls nicht: rpm-ostree install flatpak (Neustart nötig).",
        PackageSourceKind.Homebrew =>
            "Homebrew fehlt? Auf Bazzite: ujust install-brew. Sonst der offizielle Installer von brew.sh.",
        PackageSourceKind.RpmOstree =>
            "rpm-ostree gibt's nur auf Atomic-Systemen (Bazzite, Silverblue, Kinoite). Auf klassischen Fedora-Editions ist es nicht vorhanden – dann diese Quelle ignorieren.",
        PackageSourceKind.Distrobox =>
            "Distrobox installieren: rpm-ostree install distrobox (Neustart) oder rootless via brew install distrobox.",
        PackageSourceKind.AppImage =>
            // AppImage ist Datei-basiert, sollte praktisch nie als „nicht verfügbar" auftauchen
            "AppImage-Scan sucht in ~/Applications, ~/AppImages, ~/.local/share/AppImages und ~/Downloads. Lege deine .AppImage in einen dieser Ordner.",
        PackageSourceKind.Pipx =>
            "pipx installieren: brew install pipx (rootless) oder rpm-ostree install pipx (System-Layer, Neustart).",
        _ => null,
    };
}
