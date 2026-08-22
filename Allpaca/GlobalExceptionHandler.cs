using Avalonia.Threading;
using NLog;

namespace Allpaca;

/// <summary>
/// Fängt Exceptions ab, die sonst still den Prozess beenden würden: unbehandelte
/// AppDomain-Exceptions, nicht beobachtete Task-Exceptions und Fehler aus dem
/// Avalonia-Dispatcher. Alles landet als Fatal/Error im Log statt im Nichts.
/// </summary>
public static class GlobalExceptionHandler
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unbehandelte Exception (Terminating={0})", e.IsTerminating);
            LogManager.Flush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unbeobachtete Task-Exception");
            // Beobachtet markieren: eine verschluckte Hintergrund-Task soll die App
            // nicht abschießen, geloggt ist sie jetzt.
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unbehandelte Exception im UI-Thread");
            // Handled=true hält die App am Leben. Allpaca ist ein Inventar-Werkzeug --
            // ein kaputter Klick darf nicht die ganze Sitzung kosten.
            e.Handled = true;
        };
    }
}
