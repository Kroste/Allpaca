using System;
using Avalonia;
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
    /// <summary>Breite des unsichtbaren Resize-Streifens an jeder Fensterkante (DIPs).
    /// 6 ist ein guter Kompromiss zwischen Treffgenauigkeit und nicht-stoeren bei
    /// Klicks knapp neben Buttons.</summary>
    private const double EdgeMargin = 6;

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

        // Auf KDE/Wayland (Bazzite) liefert BorderOnly oft KEINE nutzbaren Resize-Griffe -
        // der "Border" ist 1 px breit und praktisch nicht treffbar. Wir schieben deshalb
        // eine Tunnel-Phase vor alle Child-Handler und mappen Klicks in der aeusseren
        // EdgeMargin-Zone auf BeginResizeDrag. Tunnel laeuft VOR Bubble, damit z. B. der
        // Titelleisten-Drag nicht zuerst zuschnappt.
        AddHandler(PointerPressedEvent, OnEdgeResizePressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnEdgeCursorMoved, RoutingStrategies.Tunnel);
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

    private void OnEdgeResizePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        if (WindowState == WindowState.Maximized) return;
        var pp = e.GetCurrentPoint(this);
        if (!pp.Properties.IsLeftButtonPressed) return;

        var edge = DetectEdge(pp.Position, ClientSize);
        if (edge is null) return;

        BeginResizeDrag(edge.Value, e);
        e.Handled = true;
    }

    private void OnEdgeCursorMoved(object? sender, PointerEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            Cursor = Cursor.Default;
            return;
        }

        var pos = e.GetCurrentPoint(this).Position;
        var edge = DetectEdge(pos, ClientSize);
        Cursor = edge switch
        {
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default,
        };
    }

    private static WindowEdge? DetectEdge(Point pos, Size size)
    {
        var left = pos.X <= EdgeMargin;
        var right = pos.X >= size.Width - EdgeMargin;
        var top = pos.Y <= EdgeMargin;
        var bottom = pos.Y >= size.Height - EdgeMargin;

        // Ecken zuerst pruefen (haben Vorrang vor den Kanten).
        if (top && left) return WindowEdge.NorthWest;
        if (top && right) return WindowEdge.NorthEast;
        if (bottom && left) return WindowEdge.SouthWest;
        if (bottom && right) return WindowEdge.SouthEast;
        if (top) return WindowEdge.North;
        if (bottom) return WindowEdge.South;
        if (left) return WindowEdge.West;
        if (right) return WindowEdge.East;
        return null;
    }
}
