using Allpaca.Services;
using Avalonia.Controls;
using Avalonia.Threading;
using NLog;

namespace Allpaca.Views;

/// <summary>
/// Legt das Hauptfenster beim Minimieren in den System-Tray. Schließen (✕) beendet
/// die App weiterhin regulär -- dafür ist kein Umbau des ShutdownMode nötig.
/// </summary>
/// <remarks>
/// Vier Absicherungen, die der Kroste-Standard verlangt und die alle schon einmal
/// weh getan haben: die Instanz wird von der App als Feld gehalten (sonst holt der
/// GC das TrayIcon weg), das Wiederherstellen läuft über ein Guard-Flag plus
/// <see cref="Dispatcher.UIThread"/>, und der ganze Aufbau steht in einem try/catch --
/// auf einem headless Server oder mit kaputtem DBus gibt es keinen Tray, dann bleibt
/// es beim normalen Minimieren.
/// </remarks>
public sealed class TrayController : IDisposable
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly Window _window;
    private TrayIcon? _tray;
    private bool _restoring;

    public TrayController(Window window)
    {
        _window = window;

        try
        {
            _tray = new TrayIcon
            {
                Icon = AppIcon.WindowIcon,
                ToolTipText = AppInfo.Name,
                IsVisible = true,
                Menu = BuildMenu(),
            };
            _tray.Clicked += (_, _) => Restore();

            _window.PropertyChanged += OnWindowPropertyChanged;
            Log.Info("System-Tray aktiv");
        }
        catch (Exception ex)
        {
            // Kein Tray (headless, kaputtes DBus): die App muss trotzdem laufen.
            Log.Warn(ex, "System-Tray nicht verfügbar - Minimieren bleibt Standardverhalten");
            _tray = null;
        }
    }

    private NativeMenu BuildMenu()
    {
        var show = new NativeMenuItem("Anzeigen");
        show.Click += (_, _) => Restore();

        var quit = new NativeMenuItem("Beenden");
        quit.Click += (_, _) =>
        {
            Log.Info("Beenden über das Tray-Menü");
            Dispatcher.UIThread.Post(() => _window.Close());
        };

        var menu = new NativeMenu();
        menu.Add(show);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quit);
        return menu;
    }

    private void OnWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty || _tray is null) return;
        if (_restoring) return;

        if (_window.WindowState == WindowState.Minimized)
        {
            Log.Debug("Fenster minimiert - ab in den Tray");
            _window.Hide();
        }
    }

    private void Restore()
    {
        // Guard + Post: das Zurücksetzen des WindowState feuert PropertyChanged erneut,
        // und der Klick kommt nicht zwingend vom UI-Thread.
        Dispatcher.UIThread.Post(() =>
        {
            if (_restoring) return;
            _restoring = true;
            try
            {
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Fenster konnte nicht wiederhergestellt werden");
            }
            finally
            {
                _restoring = false;
            }
        });
    }

    public void Dispose()
    {
        _window.PropertyChanged -= OnWindowPropertyChanged;
        _tray?.Dispose();
        _tray = null;
    }
}
