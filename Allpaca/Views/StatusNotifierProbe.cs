using System.Diagnostics;
using NLog;

namespace Allpaca.Views;

/// <summary>
/// Prüft, ob auf dem Session-Bus tatsächlich ein Tray-Host lauscht
/// (<c>org.kde.StatusNotifierWatcher</c> bzw. der freedesktop-Name).
/// </summary>
/// <remarks>
/// Nötig, weil <c>new TrayIcon(...)</c> unter Linux NICHT wirft, wenn niemand das
/// Icon anzeigen kann -- Avalonia legt es an, es erscheint nur nirgends. Ein
/// try/catch allein reicht als Absicherung also nicht: Ohne diese Probe versteckt
/// sich das Fenster beim Minimieren in einen Tray, den es nicht gibt, verliert
/// seinen Taskleisten-Eintrag und ist nur noch per <c>kill</c> erreichbar. Betrifft
/// GNOME ohne AppIndicator-Extension und jede Session ohne Watcher; auf KDE
/// (Bazzite) ist der Watcher immer da.
/// </remarks>
internal static class StatusNotifierProbe
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly string[] WatcherNames =
    [
        "org.kde.StatusNotifierWatcher",
        "org.freedesktop.StatusNotifierWatcher",
    ];

    /// <summary>
    /// True, wenn ein Tray-Host erreichbar ist. Im Zweifel <c>false</c> -- lieber ein
    /// Fenster zu viel in der Taskleiste als eines, das niemand zurückholen kann.
    /// </summary>
    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsLinux()) return true;

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
        {
            Log.Info("Kein DBUS_SESSION_BUS_ADDRESS - kein Tray-Host erreichbar");
            return false;
        }

        foreach (var name in WatcherNames)
        {
            if (HasOwner(name))
            {
                Log.Debug("Tray-Host gefunden: {0}", name);
                return true;
            }
        }

        Log.Info("Kein StatusNotifierWatcher auf dem Session-Bus - Tray wird nicht genutzt");
        return false;
    }

    private static bool HasOwner(string busName)
    {
        try
        {
            // Bewusst KEIN ProcessRunner: der leitet über flatpak-spawn --host um, und
            // gefragt ist der Session-Bus DIESES Prozesses, nicht der des Hosts.
            var psi = new ProcessStartInfo("gdbus")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("call");
            psi.ArgumentList.Add("--session");
            psi.ArgumentList.Add("--dest");
            psi.ArgumentList.Add("org.freedesktop.DBus");
            psi.ArgumentList.Add("--object-path");
            psi.ArgumentList.Add("/org/freedesktop/DBus");
            psi.ArgumentList.Add("--method");
            psi.ArgumentList.Add("org.freedesktop.DBus.NameHasOwner");
            psi.ArgumentList.Add(busName);

            using var p = Process.Start(psi);
            if (p is null) return false;

            var stdout = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(3000))
            {
                try { p.Kill(); } catch { /* egal, wir antworten sowieso mit false */ }
                return false;
            }

            return p.ExitCode == 0 && stdout.Contains("true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // gdbus fehlt oder ist nicht ausführbar: dann eben kein Tray.
            Log.Debug(ex, "StatusNotifierWatcher-Probe für {0} nicht möglich", busName);
            return false;
        }
    }
}
