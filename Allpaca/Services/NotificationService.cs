using NLog;

namespace Allpaca.Services;

/// <summary>
/// Verschickt Desktop-Notifications via notify-send. Auf Bazzite landet das im
/// KDE-Plasma-Notification-Stack; auf GNOME analog. ProcessRunner haengt
/// flatpak-spawn --host davor, sobald wir in der Distrobox laufen, sodass die
/// Notification auf dem Host-Notification-Bus erscheint statt im Sandbox-Nichts.
/// </summary>
public sealed class NotificationService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly ProcessRunner _runner;

    public NotificationService(ProcessRunner runner) => _runner = runner;

    /// <summary>Schickt eine Standard-Notification. Schlucke alle Fehler - eine
    /// nicht-funktionierende Notification soll Allpaca nicht zum Stoppen bringen.</summary>
    public async Task NotifyAsync(string title, string body, CancellationToken ct = default)
    {
        try
        {
            var r = await _runner.RunAsync("notify-send", new[]
            {
                "--app-name=Allpaca",
                // Freedesktop-Theme-Icon, ueberall verfuegbar. Spaeter koennten wir
                // unser eigenes ~/.local/share/icons/hicolor/<size>/apps/allpaca.png
                // installieren und hier referenzieren.
                "--icon=system-software-update",
                "--expire-time=8000",
                title,
                body,
            }, ct);

            if (!r.Success)
                Log.Debug("notify-send fehlgeschlagen ({0}): {1}", r.ExitCode, r.StdErr);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "notify-send nicht aufrufbar");
        }
    }
}
