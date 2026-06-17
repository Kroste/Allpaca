namespace Allpaca.Models;

/// <summary>Die unterstuetzten Installationsquellen unter Bazzite.</summary>
public enum PackageSourceKind
{
    Flatpak,
    Homebrew,
    RpmOstree,
    Distrobox,
    AppImage,
    Pipx
}
