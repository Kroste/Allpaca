using System.Threading.Tasks;
using Allpaca.Chrome;
using Allpaca.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Allpaca.Views;

public partial class ConfirmWindow : ChromeWindow
{
    public ConfirmWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Bequemer Aufruf-Entry-Point: öffnet modal, liefert true bei Bestätigung,
    /// false bei Abbruch / X-Klick / Window-Manager-Close.
    /// </summary>
    public static async Task<bool> AskAsync(Window owner, ConfirmRequest request)
    {
        var win = new ConfirmWindow { DataContext = request };
        return await win.ShowDialog<bool>(owner);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
