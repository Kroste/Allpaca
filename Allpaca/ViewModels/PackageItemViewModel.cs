using Allpaca.Models;
using Avalonia.Media;

namespace Allpaca.ViewModels;

public sealed class PackageItemViewModel
{
    public PackageInfo Model { get; }

    public PackageItemViewModel(PackageInfo model) => Model = model;

    public string Name => Model.Name;
    public string Id => Model.Id;
    public string Version => Model.Version ?? "—";
    public string Scope => Model.Scope ?? "";
    public string Description => Model.Description ?? "";
    public string Origin => Model.Origin ?? "";

    public string SizeText => Model.SizeBytes is { } b ? FormatSize(b) : "";

    public string SourceLabel => Model.Source switch
    {
        PackageSourceKind.Flatpak => "Flatpak",
        PackageSourceKind.Homebrew => "Homebrew",
        PackageSourceKind.RpmOstree => "rpm-ostree",
        PackageSourceKind.Distrobox => "Distrobox",
        PackageSourceKind.AppImage => "AppImage",
        _ => "?",
    };

    public IBrush SourceBrush => new SolidColorBrush(Color.Parse(ColorFor(Model.Source)));

    public static string ColorFor(PackageSourceKind k) => k switch
    {
        PackageSourceKind.Flatpak => "#4A90D9",
        PackageSourceKind.Homebrew => "#F5A623",
        PackageSourceKind.RpmOstree => "#E25555",
        PackageSourceKind.Distrobox => "#7B61FF",
        PackageSourceKind.AppImage => "#2BB673",
        _ => "#888888",
    };

    private static string FormatSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes;
        int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#} {1}", s, u[i]);
    }
}
