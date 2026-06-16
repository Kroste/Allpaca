using System;
using System.Diagnostics;
using System.Reflection;
using Allpaca.Chrome;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Allpaca.Views;

public partial class InfoWindow : ChromeWindow
{
    // TODO(Lars): echten GitHub-Slug und Buy-me-a-coffee-Handle eintragen.
    private const string GithubUrl = "https://github.com/Kroste/Allpaca";
    private const string CoffeeUrl = "https://www.buymeacoffee.com/kroste";

    public InfoWindow()
    {
        InitializeComponent();

        var v = Assembly.GetExecutingAssembly().GetName().Version;
        if (this.FindControl<TextBlock>("VersionText") is { } vt)
            vt.Text = "Version " + (v?.ToString(3) ?? "1.0");
    }

    private void OnGithubClick(object? sender, RoutedEventArgs e) => OpenUrl(GithubUrl);
    private void OnCoffeeClick(object? sender, RoutedEventArgs e) => OpenUrl(CoffeeUrl);

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
        catch { /* nicht weiter behandelbar */ }
    }
}
