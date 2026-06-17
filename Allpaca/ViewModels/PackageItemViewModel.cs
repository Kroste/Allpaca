using System.IO;
using Allpaca.Models;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NLog;
using SkiaSharp;
using Svg.Skia;

namespace Allpaca.ViewModels;

public sealed class PackageItemViewModel
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public PackageInfo Model { get; }

    public PackageItemViewModel(PackageInfo model) => Model = model;

    private Bitmap? _icon;
    private bool _iconLoaded;

    /// <summary>Per-App-Icon (Flatpak hicolor, AppImage .desktop Icon=). Lazy beim
    /// ersten Zugriff geladen. PNG direkt via Avalonia.Bitmap, SVG via Svg.Skia
    /// rastert auf 64x64 (ListBox zeigt 28x28 - leichte Skalierung haelt Antialiasing
    /// sauber). Fehler werden silent in Debug geloggt und liefern null.</summary>
    public Bitmap? Icon
    {
        get
        {
            if (_iconLoaded) return _icon;
            _iconLoaded = true;

            if (string.IsNullOrEmpty(Model.IconPath)) return null;
            try
            {
                _icon = Model.IconPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                    ? RenderSvgToBitmap(Model.IconPath, 64)
                    : new Bitmap(Model.IconPath);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Icon-Load fehlgeschlagen: {0}", Model.IconPath);
            }
            return _icon;
        }
    }

    private static Bitmap? RenderSvgToBitmap(string path, int size)
    {
        using var svg = new SKSvg();
        svg.Load(path);
        if (svg.Picture is null) return null;

        var bounds = svg.Picture.CullRect;
        var max = Math.Max(bounds.Width, bounds.Height);
        if (max <= 0) return null;
        var scale = size / max;

        using var bmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale, scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(svg.Picture);
        }

        using var image = SKImage.FromBitmap(bmp);
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        pngData.SaveTo(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    public string Name => Model.Name;
    public string Id => Model.Id;
    public string Version => Model.Version ?? "";
    public string Scope => Model.Scope ?? "";
    public string Description => Model.Description ?? "";
    public string Origin => Model.Origin ?? "";

    /// <summary>True, wenn derselbe Eintrag (nach Namens-Normalisierung) in mindestens
    /// einer weiteren Quelle ebenfalls vorkommt. Wird vom PackageDuplicateDetector gesetzt.</summary>
    public bool IsDuplicate { get; internal set; }

    /// <summary>Komma-getrennte Liste der anderen Quellen, in denen der Eintrag ebenfalls existiert
    /// (z. B. "Flatpak, AppImage"). Leer, wenn IsDuplicate=false.</summary>
    public string DuplicateInfo { get; internal set; } = "";

    /// <summary>True, wenn fuer diesen Eintrag laut CheckUpdatesAsync der Quelle ein
    /// Update bereitsteht. Wird vom MainWindowViewModel nach dem Refresh gesetzt.</summary>
    public bool HasUpdate { get; internal set; }

    public string SizeText => Model.SizeBytes is { } b ? FormatSize(b) : "";

    public string SourceLabel => Model.Source switch
    {
        PackageSourceKind.Flatpak => "Flatpak",
        PackageSourceKind.Homebrew => "Homebrew",
        PackageSourceKind.RpmOstree => "rpm-ostree",
        PackageSourceKind.Distrobox => "Distrobox",
        PackageSourceKind.AppImage => "AppImage",
        PackageSourceKind.Pipx => "pipx",
        _ => "?",
    };

    /// <summary>Bequemes Bool fuer XAML-Bindings (z. B. fuer den Drill-down-Button im Detailpanel).</summary>
    public bool IsDistrobox => Model.Source == PackageSourceKind.Distrobox;

    public IBrush SourceBrush => new SolidColorBrush(Color.Parse(ColorFor(Model.Source)));

    /// <summary>Distrobox-Container-Status (z. B. "Up 2 hours", "Exited", "Created"). Leer fuer andere Quellen.</summary>
    public string DistroboxStatus =>
        Model.Source == PackageSourceKind.Distrobox &&
        Model.Extra is { } x && x.TryGetValue("status", out var s)
            ? s
            : "";

    public IBrush DistroboxStatusForeground =>
        new SolidColorBrush(Color.Parse(StatusForegroundColor(DistroboxStatus)));

    public IBrush DistroboxStatusBackground =>
        new SolidColorBrush(Color.Parse(StatusBackgroundColor(DistroboxStatus)));

    private static string StatusForegroundColor(string status)
    {
        var s = status.ToLowerInvariant();
        if (s.StartsWith("up") || s.Contains("running")) return "#2BB673";   // gruen
        if (s.StartsWith("created") || s.StartsWith("configured")) return "#F5A623"; // gelb
        if (s.StartsWith("paused")) return "#4A90D9";                        // blau
        return "#9AA0A8";                                                    // exited/stopped/dead/unknown -> grau
    }

    private static string StatusBackgroundColor(string status)
    {
        var s = status.ToLowerInvariant();
        if (s.StartsWith("up") || s.Contains("running")) return "#1E3527";
        if (s.StartsWith("created") || s.StartsWith("configured")) return "#3A2F1B";
        if (s.StartsWith("paused")) return "#1F3046";
        return "#2A2E35";
    }

    public static string ColorFor(PackageSourceKind k) => k switch
    {
        PackageSourceKind.Flatpak => "#4A90D9",
        PackageSourceKind.Homebrew => "#F5A623",
        PackageSourceKind.RpmOstree => "#E25555",
        PackageSourceKind.Distrobox => "#7B61FF",
        PackageSourceKind.AppImage => "#2BB673",
        PackageSourceKind.Pipx => "#3776AB",  // Python-Blau
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
