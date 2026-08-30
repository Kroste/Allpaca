using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using NLog;

namespace Allpaca.Services;

/// <summary>
/// Update-Check UND echtes Self-Update gegen GitHub Releases. Eine reine
/// "Version X ist da"-Meldung reicht nach Kroste-Standard nicht -- der Nutzer muss
/// per Klick aktualisieren können.
/// </summary>
/// <remarks>
/// Allpaca ist Linux-only, deshalb gibt es nur zwei Austauschwege: das laufende
/// AppImage ersetzt sich selbst (<c>cp -f</c> auf <c>$APPIMAGE</c>, weil mv/rm am
/// gemounteten Loop-Device mit "Text file busy" scheitert), oder der tar.gz-Tarball
/// wird über das Installationsverzeichnis entpackt.
/// </remarks>
public sealed class UpdateService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly Uri LatestReleaseUrl =
        new($"https://api.github.com/repos/{AppInfo.RepoOwner}/{AppInfo.RepoName}/releases/latest");


    /// <summary>True, wenn die App als AppImage läuft (der Loader setzt $APPIMAGE).</summary>
    public static bool RunningAsAppImage =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPIMAGE"));

    /// <summary>
    /// Fragt das neueste Release ab.
    /// </summary>
    /// <remarks>
    /// Liefert bewusst ein <see cref="UpdateCheckResult"/> statt <c>UpdateRelease?</c>:
    /// "kein Update vorhanden" und "Check fehlgeschlagen" sind für den Nutzer zwei
    /// verschiedene Aussagen. Mit einem nullable Rückgabewert hätte die UI beides als
    /// "Du bist aktuell" angezeigt -- also auch dann, wenn gar nicht geprüft werden
    /// konnte, weil kein Netz da war oder GitHub das Rate-Limit gezogen hat.
    /// </remarks>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var http = CreateClient();
            Log.Debug("Update-Check gegen {0}", LatestReleaseUrl);

            var json = await http.GetStringAsync(LatestReleaseUrl, ct).ConfigureAwait(false);
            var release = UpdateReleaseParser.Parse(json);
            if (release is null)
            {
                Log.Warn("Update-Check: Release-JSON nicht lesbar");
                return UpdateCheckResult.Failed("Antwort von GitHub war nicht lesbar.");
            }

            var newer = UpdateReleaseParser.IsNewer(AppInfo.Version, release.Version);
            Log.Info("Update-Check fertig in {0} ms: installiert {1}, neuestes {2}, Update {3}",
                sw.ElapsedMilliseconds, AppInfo.Version, release.Version, newer ? "verfügbar" : "keins");

            return newer ? UpdateCheckResult.Available(release) : UpdateCheckResult.UpToDate();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Bewusst nur Warn: kein Netz, Proxy davor oder Rate-Limit sind Alltag.
            Log.Warn(ex, "Update-Check fehlgeschlagen nach {0} ms", sw.ElapsedMilliseconds);
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    /// <summary>Das zur laufenden Installationsform passende Asset, oder null.</summary>
    public static UpdateAsset? SelectAsset(UpdateRelease release)
        => UpdateReleaseParser.SelectAsset(release.Assets, RunningAsAppImage);

    /// <summary>
    /// Lädt das Asset herunter und startet das Austausch-Skript. Gibt true zurück,
    /// wenn der Installer läuft.
    /// </summary>
    /// <remarks>
    /// WICHTIG: Der Aufrufer MUSS die App bei true sofort beenden
    /// (<see cref="TerminateForUpdate"/>). Das Skript wartet per <c>kill -0</c> auf
    /// das Prozessende -- läuft die App weiter, wartet es ewig und die UI bleibt bei
    /// "100 %" stehen.
    /// </remarks>
    public async Task<bool> DownloadAndApplyAsync(
        UpdateRelease release, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var asset = SelectAsset(release);
        if (asset is null)
        {
            Log.Warn("Kein passendes Asset im Release {0} gefunden", release.TagName);
            return false;
        }

        // CreateTempSubdirectory statt eines vorhersagbaren Namens: /tmp ist
        // world-writable, und hier landet ein Skript, das gleich ausgeführt wird.
        // Ein vorab angelegter Symlink auf denselben Pfad wäre sonst ein
        // Einfallstor (TOCTOU).
        var workDir = Directory.CreateTempSubdirectory("allpaca-update-").FullName;
        var downloaded = Path.Combine(workDir, Path.GetFileName(asset.Name));

        Log.Info("Lade Update-Asset {0} ({1} Bytes) nach {2}", asset.Name, asset.Size, downloaded);
        await DownloadAsync(asset, downloaded, progress, ct).ConfigureAwait(false);

        var script = Path.Combine(workDir, "apply-update.sh");
        await File.WriteAllTextAsync(script, BuildInstallerScript(downloaded), ct).ConfigureAwait(false);
        Chmod(script, "+x");

        var psi = new ProcessStartInfo("/usr/bin/env", $"bash \"{script}\"")
        {
            UseShellExecute = false,
            WorkingDirectory = workDir,
        };
        Process.Start(psi);
        Log.Info("Installer gestartet: {0}", script);
        return true;
    }

    /// <summary>
    /// Beendet die App, damit der wartende Installer weiterlaufen kann. Muss von JEDEM
    /// Aufrufer nach einem erfolgreichen <see cref="DownloadAndApplyAsync"/> gerufen
    /// werden -- das Vergessen ist die häufigste Ursache für hängende Updates.
    /// </summary>
    public static void TerminateForUpdate()
    {
        Log.Info("Beende Allpaca für den Update-Austausch");
        LogManager.Flush();

        // Fail-Safe: falls Exit an einem Finalizer hängen bleibt, hart nachtreten.
        // Der Installer braucht kein sauberes Ende, nur das Verschwinden der PID.
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500).ConfigureAwait(false);
            Process.GetCurrentProcess().Kill();
        });

        Environment.Exit(0);
    }

    private async Task DownloadAsync(
        UpdateAsset asset, string target, IProgress<double>? progress, CancellationToken ct)
    {
        using var http = CreateClient();
        using var resp = await http
            .GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? asset.Size;
        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = File.Create(target);

        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            done += read;
            if (total > 0) progress?.Report((double)done / total);
        }
        progress?.Report(1.0);
        Log.Info("Download fertig: {0} Bytes", done);
    }

    /// <summary>
    /// Baut das Austausch-Skript. Der Log geht bewusst nach $XDG_STATE_HOME und NICHT
    /// neben die Exe: beim laufenden AppImage ist das Programmverzeichnis ein
    /// read-only Squashfs-Mount, ein "exec >>log" dorthin bricht bash sofort ab und
    /// das Update passiert nie.
    /// </summary>
    internal static string BuildInstallerScript(string downloadedFile)
    {
        var pid = Environment.ProcessId;
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        var baseDir = AppContext.BaseDirectory.TrimEnd('/');
        var exe = Path.Combine(baseDir, AppInfo.Name);

        var sb = new System.Text.StringBuilder();
        // LF hart anhängen: AppendLine wäre plattformabhängig, ein Shell-Skript mit
        // CRLF scheitert am Shebang.
        void Line(string s) => sb.Append(s).Append('\n');

        Line("#!/usr/bin/env bash");
        Line("set -u");
        Line("STATE=\"${XDG_STATE_HOME:-$HOME/.local/state}/Allpaca\"");
        Line("mkdir -p \"$STATE\" 2>/dev/null || STATE=/tmp");
        Line("exec >>\"$STATE/update.log\" 2>&1");
        Line("echo \"=== $(date -Is) Update-Austausch startet ===\"");
        Line($"PID={pid}");
        Line("for _ in $(seq 1 120); do kill -0 \"$PID\" 2>/dev/null || break; sleep 0.5; done");
        Line("sleep 1");

        // Pfade als Variablen mit Single-Quote-Escaping, nicht direkt in doppelte
        // Quotes interpoliert: ein $, ein Backtick oder ein " im Pfad (der Asset-Name
        // kommt von GitHub, $APPIMAGE vom Nutzer) würde die Zeile sonst umlenken oder
        // das Skript zerlegen.
        Line($"SRC={Quote(downloadedFile)}");

        if (!string.IsNullOrWhiteSpace(appImage))
        {
            Line($"TARGET={Quote(appImage)}");
            // cp -f statt mv/rm: das laufende AppImage ist als Loop-Device gemountet,
            // ein Verschieben scheitert mit "Text file busy". Der Inode bleibt gleich.
            Line("if cp -f \"$SRC\" \"$TARGET\"; then");
            Line("  chmod +x \"$TARGET\"");
            Line("  echo \"AppImage ersetzt\"");
            Line("else");
            Line("  echo \"FEHLER: cp nach $TARGET fehlgeschlagen - starte die alte Version\"");
            Line("fi");
            Line("setsid \"$TARGET\" >/dev/null 2>&1 &");
        }
        else
        {
            Line($"TARGET={Quote(baseDir)}");
            Line($"EXE={Quote(exe)}");
            Line("if tar -xzf \"$SRC\" -C \"$TARGET\"; then");
            Line("  chmod +x \"$EXE\" 2>/dev/null");
            Line("  echo \"Tarball entpackt\"");
            Line("else");
            Line("  echo \"FEHLER: tar nach $TARGET fehlgeschlagen - starte die alte Version\"");
            Line("fi");
            Line("setsid \"$EXE\" >/dev/null 2>&1 &");
        }

        // Der Neustart läuft in BEIDEN Fällen. Die App hat sich für den Austausch
        // schon beendet -- bricht das Skript hier mit exit 1 ab, ist sie einfach weg,
        // ohne Update und ohne dass jemand den Grund sieht. Lieber die alte Version
        // zurückholen; die Ursache steht im update.log.
        Line("echo \"=== Austausch fertig ===\"");
        Line("exit 0");
        return sb.ToString();
    }

    /// <summary>Verpackt einen Pfad in Single Quotes, POSIX-konform escaped.</summary>
    private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static HttpClient CreateClient()
    {
        // Proxy-aware: auf Bazzite ein No-Op, hinter einem Firmen-Proxy notwendig.
        var handler = new HttpClientHandler
        {
            Proxy = WebRequest.DefaultWebProxy,
            DefaultProxyCredentials = CredentialCache.DefaultCredentials,
        };

        var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        // Die GitHub-API antwortet ohne User-Agent mit 403.
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.Version));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    private static void Chmod(string path, string mode)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("chmod", $"{mode} \"{path}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
            });
            p?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "chmod auf {0} fehlgeschlagen", path);
        }
    }
}
