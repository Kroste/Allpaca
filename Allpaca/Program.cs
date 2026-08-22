using Avalonia;
using NLog;

namespace Allpaca;

internal static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        // Der masked-Renderer registriert sich über einen [ModuleInitializer] in
        // MaskingLayoutRenderer -- damit greift er auch im Testprozess, der kein Main hat.
        var log = LogManager.GetCurrentClassLogger();
        GlobalExceptionHandler.Install();

        try
        {
            // Icon-Export für scripts/build_icon.sh: rendert das Logo aus AppIcon in
            // eine PNG-Datei. Bewusst kein zweites Pillow-Skript wie in anderen
            // Projekten -- das Motiv lebt im SkiaSharp-Renderer und soll genau EINE
            // Quelle der Wahrheit haben.
            if (args.Length >= 2 && args[0] == "--export-icon")
            {
                Services.AppIcon.ExportPng(args[1], size: 256);
                log.Info("Icon exportiert nach {0}", args[1]);
                return;
            }

            log.Info("Allpaca startet (Version {0})", AppInfo.Version);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            log.Info("Allpaca beendet");
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "Absturz beim Start");
            throw;
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
