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
        catch (JsonException ex)
        {
            // NUR bei kaputtem JSON quarantänisieren: der Inhalt ist nachweislich
            // unbrauchbar, und ohne das Wegräumen würde der nächste Save ihn
            // kommentarlos überschreiben.
            Log.Error(ex, "Settings sind kein gültiges JSON ({0}) - werden als .broken gesichert", Path);
            Quarantine();
            return new AppSettings();
        }
        catch (Exception ex)
        {
            // IO-Fehler (Datei gesperrt, Verzeichnis kurz weg): der Inhalt ist
            // vermutlich intakt. Hier NICHT wegräumen, sonst zerstört die
            // Schutzmaßnahme genau die Daten, die sie retten soll.
            Log.Warn(ex, "Settings konnten nicht gelesen werden ({0}) - Defaults für diese Sitzung", Path);
            return new AppSettings();
        }
    }

    /// <summary>Schiebt eine unlesbare Settings-Datei zur Seite, statt sie zu verlieren.</summary>
    private void Quarantine()
    {
        try
        {
            var broken = Path + ".broken";
            File.Move(Path, broken, overwrite: true);
            Log.Info("Kaputte Settings gesichert unter {0}", broken);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Kaputte Settings konnten nicht gesichert werden");
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, JsonOptions);

            // Atomar schreiben: erst vollständig in die .tmp, dann in einem Rutsch
            // über das Ziel bewegen. Ein Absturz mitten im Schreiben hinterlässt so
            // die alte, gültige Datei statt einer halben.
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, Path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Settings konnten nicht gespeichert werden ({0})", Path);
        }
    }
}
