using System.Text.Json;
using NLog;

namespace Allpaca.Services;

/// <summary>
/// Liest und schreibt <see cref="AppSettings"/>. Pfad-Logik: bevorzugt
/// $XDG_CONFIG_HOME/Allpaca/settings.json, Fallback ~/.config/Allpaca/settings.json.
/// Fehler beim Lesen/Schreiben werden geloggt und schlucken silent - die App soll
/// auch ohne Persistenz funktionieren.
/// </summary>
public sealed class SettingsService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Path { get; }

    public SettingsService(string? overridePath = null)
    {
        Path = overridePath ?? DefaultPath();
    }

    public static string DefaultPath()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configDir = string.IsNullOrWhiteSpace(xdg)
            ? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config")
            : xdg;
        return System.IO.Path.Combine(configDir, "Allpaca", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new AppSettings();

            var json = File.ReadAllText(Path);
            if (string.IsNullOrWhiteSpace(json)) return new AppSettings();

            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return loaded ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Settings konnten nicht geladen werden ({0}) - Defaults", Path);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(Path, json);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Settings konnten nicht gespeichert werden ({0})", Path);
        }
    }
}
