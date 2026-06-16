using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Allpaca.Chrome;

/// <summary>
/// Basisfenster mit eigenem Chrome (randlos, eigene Titelleiste, resizable,
/// sauberes Shutdown). Konvention wie in Magnat/NetScanner: alle Fenster
/// erben hiervon und liefern ihre Titelleiste im XAML, die diese Handler nutzt.
/// </summary>
public class ChromeWindow : Window
{
    public ChromeWindow()
    {
        // Avalonia 12: ExtendClientAreaChromeHints ist ENTFALLEN. Custom-Chrome läuft
        // jetzt über WindowDecorations.BorderOnly (blendet nur die gezeichnete Titelleiste
        // aus) + ExtendClientAreaToDecorationsHint. WindowDecorations.None würde die
        // nativen Resize-Griffe entfernen – deshalb BorderOnly.
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;
        WindowDecorations = WindowDecorations.BorderOnly;
        CanResize = true;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ClampToWorkingArea();
    }

    /// <summary>
    /// Verhindert, dass ein Fenster groesser als der Arbeitsbereich des aktiven
    /// Bildschirms oeffnet (z. B. auf einem kleineren 2. Monitor). Width/Height
    /// sind DIPs, WorkingArea ist in physischen Pixeln – daher durch Scaling teilen.
    /// </summary>
    protected void ClampToWorkingArea(double maxFraction = 0.9)
    {
        var screen = Screens?.ScreenFromVisual(this) ?? Screens?.Primary;
        if (screen is null) return;

        var scaling = screen.Scaling <= 0 ? 1.0 : screen.Scaling;
        var maxW = screen.WorkingArea.Width / scaling;
        var maxH = screen.WorkingArea.Height / scaling;

        if (Width > maxW * 0.98) Width = maxW * maxFraction;
        if (Height > maxH * 0.98) Height = maxH * maxFraction;
    }

    protected void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2)
            ToggleMaximize();
        else
            BeginMoveDrag(e);
    }

    protected void OnMinimizeClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    protected void OnMaximizeClick(object? sender, RoutedEventArgs e)
        => ToggleMaximize();

    protected void OnCloseClick(object? sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}
