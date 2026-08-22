using Allpaca.Services;
using Allpaca.Services.Sources;
using Allpaca.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Allpaca;

/// <summary>
/// Komposition der App: hier -- und nur hier -- wird verdrahtet, wer wen bekommt.
/// Vorher hat sich das MainWindowViewModel seine Quellen selbst zusammengebaut,
/// was es untestbar gemacht hat.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<SandboxDetector>();
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<NotificationService>();
        // SettingsService hat einen optionalen Pfad-Parameter, den DI nicht auflösen
        // kann -- deshalb explizit über eine Factory.
        services.AddSingleton(_ => new SettingsService());
        services.AddSingleton<UpdateService>();

        // Jede Paketquelle ist eine IPackageSource -- eine neue Quelle wird hier
        // registriert und sonst nirgends angefasst.
        services.AddSingleton<IPackageSource, FlatpakSource>();
        services.AddSingleton<IPackageSource, HomebrewSource>();
        services.AddSingleton<IPackageSource, RpmOstreeSource>();
        services.AddSingleton<IPackageSource, DistroboxSource>();
        services.AddSingleton<IPackageSource, AppImageSource>();
        services.AddSingleton<IPackageSource, PipxSource>();

        services.AddSingleton<PackageAggregator>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
