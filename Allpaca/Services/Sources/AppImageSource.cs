using System.Runtime.CompilerServices;
using Allpaca.Models;
using Allpaca.Services;
using NLog;

namespace Allpaca.Services.Sources;

/// <summary>
/// Erkennt AppImages ueber zwei Wege: integrierte .desktop-Eintraege (z. B.
/// via Gear Lever), deren Exec auf eine .AppImage zeigt, sowie lose
/// .AppImage-Dateien in ueblichen Verzeichnissen. Rein dateisystembasiert,
/// daher kein CLI-Tool noetig.
/// </summary>
public sealed class AppImageSource : IPackageSource
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public PackageSourceKind Kind => PackageSourceKind.AppImage;
    public string DisplayName => "AppImage";
    public PackageCapabilities Capabilities => new()
    {
        CanSearch = false, CanInstall = false, CanUninstall = true, CanUpdate = false,
        RequiresRoot = false, RequiresReboot = false,
    };

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var home = Home();
        var apps = Path.Combine(home, ".local/share/applications");
        var available = Directory.Exists(apps) || SearchDirs(home).Any(Directory.Exists);
        return Task.FromResult(available);
    }

    public Task<IReadOnlyList<PackageInfo>> ListInstalledAsync(CancellationToken ct = default)
    {
        var home = Home();
        var found = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);

        // 1) Integrierte .desktop-Eintraege mit AppImage-Exec.
        var apps = Path.Combine(home, ".local/share/applications");
        if (Directory.Exists(apps))
        {
            foreach (var file in Directory.EnumerateFiles(apps, "*.desktop"))
            {
                try
                {
                    var (name, exec, comment, icon) = ParseDesktop(file);
                    if (exec is null || !exec.Contains(".appimage", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var path = ExtractExecPath(exec);
                    var key = path ?? file;
                    found[key] = new PackageInfo
                    {
                        Id = key,
                        Name = name ?? Path.GetFileNameWithoutExtension(key),
                        Source = Kind,
                        Description = comment,
                        Origin = path,
                        Scope = "integriert",
                        SizeBytes = SafeSize(path),
                        IconPath = ResolveIcon(icon),
                    };
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Desktop-Parse fehlgeschlagen: {0}", file);
                }
            }
        }

        // 2) Lose .AppImage-Dateien in ueblichen Ordnern.
        foreach (var dir in SearchDirs(home).Where(Directory.Exists))
        {
            foreach (var ai in Directory.EnumerateFiles(dir, "*.AppImage", SearchOption.TopDirectoryOnly))
            {
                if (found.ContainsKey(ai)) continue;
                found[ai] = new PackageInfo
                {
                    Id = ai,
                    Name = Path.GetFileNameWithoutExtension(ai),
                    Source = Kind,
                    Origin = ai,
                    Scope = "nicht integriert",
                    SizeBytes = SafeSize(ai),
                };
            }
        }

        return Task.FromResult<IReadOnlyList<PackageInfo>>(found.Values.ToList());
    }

    private static string Home() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static IEnumerable<string> SearchDirs(string home)
    {
        yield return Path.Combine(home, "Applications");
        yield return Path.Combine(home, "AppImages");
        yield return Path.Combine(home, ".local/share/AppImages");
        yield return Path.Combine(home, "Downloads");
    }

    private static long? SafeSize(string? path)
    {
        try { return path is not null && File.Exists(path) ? new FileInfo(path).Length : null; }
        catch { return null; }
    }

    private static (string? name, string? exec, string? comment, string? icon) ParseDesktop(string file)
    {
        string? name = null, exec = null, comment = null, icon = null;
        foreach (var raw in File.ReadLines(file))
        {
            var l = raw.Trim();
            if (name is null && l.StartsWith("Name=", StringComparison.Ordinal)) name = l[5..];
            else if (exec is null && l.StartsWith("Exec=", StringComparison.Ordinal)) exec = l[5..];
            else if (comment is null && l.StartsWith("Comment=", StringComparison.Ordinal)) comment = l[8..];
            else if (icon is null && l.StartsWith("Icon=", StringComparison.Ordinal)) icon = l[5..];
        }
        return (name, exec, comment, icon);
    }

    /// <summary>Icon= im .desktop kann ein absoluter PNG-Pfad sein oder ein Theme-Name -
    /// dann via IconLookup in hicolor aufloesen.</summary>
    internal static string? ResolveIcon(string? iconValue)
    {
        if (string.IsNullOrWhiteSpace(iconValue)) return null;
        var icon = iconValue.Trim();
        if (icon.Contains('/'))
            return File.Exists(icon) ? icon : null;
        return IconLookup.FindPng(icon);
    }

    internal static string? ExtractExecPath(string exec)
    {
        var trimmed = exec.Trim();
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            if (end > 1) return trimmed[1..end];
        }
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    public Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PackageInfo>>(Array.Empty<PackageInfo>());

    public IAsyncEnumerable<ProgressLine> InstallAsync(string id, CancellationToken ct = default)
        => EmptyStream();

    public async IAsyncEnumerable<ProgressLine> UninstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;

        // 1) AppImage-Datei selbst loeschen.
        yield return DeleteFileSafe(id, "Entfernt", "Datei nicht gefunden", "Loeschen fehlgeschlagen");

        // 2) Waisen-.desktop-Eintraege aufraeumen, sonst bleibt der Menueeintrag mit
        //    totem Exec-Pfad stehen. Nur ~/.local/share/applications - systemweite
        //    Eintraege (z. B. /usr/share/applications) ruehren wir bewusst nicht an.
        foreach (var orphan in FindMatchingDesktopEntries(id))
            yield return DeleteFileSafe(orphan, "Menüeintrag entfernt", "Menüeintrag fehlt", "Menüeintrag konnte nicht entfernt werden");
    }

    public IAsyncEnumerable<ProgressLine> UpdateAsync(string? id, CancellationToken ct = default)
        => EmptyStream();

    /// <summary>Loescht eine Datei und liefert eine fertige ProgressLine zurueck -
    /// kapselt das try/catch, damit der Aufrufer das in iterator-Methoden mit yield
    /// kombinieren kann (yield in try/catch ist in C# nicht erlaubt).</summary>
    private static ProgressLine DeleteFileSafe(string path, string okLabel, string notFoundLabel, string errLabel)
    {
        if (!File.Exists(path))
            return new ProgressLine($"{notFoundLabel}: {path}", true);
        try
        {
            File.Delete(path);
            return new ProgressLine($"{okLabel}: {path}", false);
        }
        catch (Exception ex)
        {
            return new ProgressLine($"{errLabel}: {path} ({ex.Message})", true);
        }
    }

    private static IEnumerable<string> FindMatchingDesktopEntries(string appImagePath)
    {
        var apps = Path.Combine(Home(), ".local/share/applications");
        if (!Directory.Exists(apps)) yield break;

        foreach (var desktop in Directory.EnumerateFiles(apps, "*.desktop"))
        {
            string? exec = null;
            try { (_, exec, _, _) = ParseDesktop(desktop); }
            catch (Exception ex) { Log.Debug(ex, "Desktop-Parse fehlgeschlagen: {0}", desktop); }

            if (ExecMatchesAppImage(exec, appImagePath))
                yield return desktop;
        }
    }

    /// <summary>True, wenn der Exec-Wert auf den gegebenen AppImage-Pfad zeigt
    /// (ein wenig tolerant gegen Quoting und %U-Argumente).</summary>
    internal static bool ExecMatchesAppImage(string? execValue, string appImagePath)
    {
        if (execValue is null) return false;
        var path = ExtractExecPath(execValue);
        return string.Equals(path, appImagePath, StringComparison.Ordinal);
    }

    private static async IAsyncEnumerable<ProgressLine> EmptyStream()
    {
        await Task.CompletedTask;
        yield break;
    }
}
