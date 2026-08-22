namespace Allpaca.Models;

/// <summary>Die unterstützten Installationsquellen unter Bazzite.</summary>
public enum PackageSourceKind
{
    Flatpak,
    Homebrew,
    RpmOstree,
    Distrobox,
    AppImage,
    Pipx
}
