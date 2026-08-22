using Allpaca.ViewModels;
using Allpaca.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Allpaca;

public partial class App : Application
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>Muss als Feld leben: sonst sammelt der GC das TrayIcon ein und
    /// das Symbol verschwindet nach ein paar Minuten aus der Leiste.</summary>
    private TrayController? _tray;

    public IServiceProvider? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = ServiceRegistration.Build();

            var window = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.MainWindow = window;

            // Nach der Fenster-Erzeugung, sonst gibt es nichts zum Minimieren.
            _tray = new TrayController(window);

            desktop.Exit += (_, _) =>
            {
                Log.Info("Shutdown - Tray wird abgeräumt");
                _tray?.Dispose();
                _tray = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
