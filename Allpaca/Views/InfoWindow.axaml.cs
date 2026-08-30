using System.Diagnostics;
using Allpaca.Chrome;
using Allpaca.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Allpaca.Views;

public partial class InfoWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly UpdateService _updates;
    private UpdateRelease? _pending;

    /// <summary>Avalonia erzeugt Fenster parameterlos -- den UpdateService holt sich
    /// das Fenster deshalb aus dem DI-Container (DoD-Punkt 9). Der <c>new</c>-Fallback
    /// greift nur im XAML-Designer, wo keine App-Instanz läuft.</summary>
    public InfoWindow() : this(App.Services?.GetService<UpdateService>() ?? new UpdateService()) { }

    public InfoWindow(UpdateService updates)
    {
        InitializeComponent();
        _updates = updates;

        if (this.FindControl<TextBlock>("VersionText") is { } vt)
            vt.Text = "Version " + AppInfo.Version;
    }

    private async void OnCheckUpdateClick(object? sender, RoutedEventArgs e)
    {
        var button = this.FindControl<Button>("CheckUpdateButton");
        var status = this.FindControl<TextBlock>("UpdateStatusText");
        var install = this.FindControl<Button>("InstallUpdateButton");
        if (status is null) return;

        if (button is not null) button.IsEnabled = false;
        status.IsVisible = true;
        status.Text = "Suche nach Updates …";
        if (install is not null) install.IsVisible = false;

        try
        {
            var result = await _updates.CheckAsync();
            _pending = null;

            if (result.IsError)
            {
                // Nicht als "du bist aktuell" verkaufen: geprüft wurde gar nichts.
                status.Text = $"Update-Check nicht möglich: {result.Error}";
                return;
            }

            if (!result.HasUpdate)
            {
                status.Text = $"Allpaca {AppInfo.Version} ist aktuell.";
                return;
            }

            var release = result.Release!;
            _pending = release;
            var asset = UpdateService.SelectAsset(release);
            if (asset is null)
            {
                // Kein passendes Paket für diese Installationsform -> Release-Seite anbieten,
                // statt so zu tun, als könnte die App sich selbst austauschen.
                status.Text = $"Version {release.Version} ist verfügbar, aber ohne passendes "
                            + "Paket für diese Installation. Öffne die Release-Seite.";
                Log.Warn("Update {0} ohne passendes Asset (AppImage={1})",
                    release.Version, UpdateService.RunningAsAppImage);
                return;
            }

            status.Text = $"Version {release.Version} ist verfügbar (installiert: {AppInfo.Version}).";
            if (install is not null) install.IsVisible = true;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Update-Check aus dem Info-Fenster fehlgeschlagen");
            status.Text = "Update-Check fehlgeschlagen - siehe Log.";
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }

    private async void OnInstallUpdateClick(object? sender, RoutedEventArgs e)
    {
        if (_pending is null) return;

        var status = this.FindControl<TextBlock>("UpdateStatusText");
        var bar = this.FindControl<ProgressBar>("UpdateProgress");
        var install = this.FindControl<Button>("InstallUpdateButton");

        var confirmed = await ConfirmWindow.AskAsync(this, new ViewModels.ConfirmRequest(
            "Update installieren?",
            $"Allpaca {_pending.Version} wird heruntergeladen und ersetzt die laufende "
            + "Version. Die App startet dabei neu.",
            "Installieren",
            IsDestructive: false));
        if (!confirmed) return;

        if (install is not null) install.IsEnabled = false;
        if (bar is not null) bar.IsVisible = true;

        var progress = new Progress<double>(p =>
            Dispatcher.UIThread.Post(() =>
            {
                if (bar is not null) bar.Value = p;
                if (status is not null) status.Text = $"Update lädt … {p:P0}";
            }));

        try
        {
            var ok = await _updates.DownloadAndApplyAsync(_pending, progress);
            if (!ok)
            {
                if (status is not null) status.Text = "Update konnte nicht vorbereitet werden - siehe Log.";
                if (install is not null) install.IsEnabled = true;
                return;
            }

            // PFLICHT: der Installer wartet per kill -0 auf das Prozessende. Ohne den
            // Self-Exit hängt die Anzeige bei 100 % und das Update passiert nie.
            if (status is not null) status.Text = "Update wird angewendet - Allpaca startet neu …";
            UpdateService.TerminateForUpdate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update-Installation fehlgeschlagen");
            if (status is not null) status.Text = "Update fehlgeschlagen - siehe Log.";
            if (install is not null) install.IsEnabled = true;
        }
    }

    private void OnGithubClick(object? sender, RoutedEventArgs e) => OpenUrl(AppInfo.GithubUrl);
    private void OnCoffeeClick(object? sender, RoutedEventArgs e) => OpenUrl(AppInfo.CoffeeUrl);

    private void OpenUrl(string url)
    {
        // Bevorzugt der Avalonia-Launcher (nutzt unter Linux das xdg-Portal),
        // Fallback auf den System-Handler.
        try
        {
            if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                _ = launcher.LaunchUriAsync(new Uri(url));
                return;
            }
        }
        catch { /* Fallback unten */ }

        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn(ex, "URL konnte nicht geöffnet werden: {0}", url); }
    }
}
