using System;
using Allpaca.Chrome;
using Allpaca.ViewModels;

namespace Allpaca.Views;

public partial class MainWindow : ChromeWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel vm)
        {
            // ViewModel kennt keine View-Typen - "oeffne ein LogWindow" und "frag den
            // User per modalem Dialog" werden hier injiziert. DataContext steht erst in
            // OnOpened sicher (siehe App.OnFrameworkInitializationCompleted).
            vm.RunOperation ??= RunOperationAsync;
            vm.ConfirmAsync ??= req => ConfirmWindow.AskAsync(this, req);

            if (vm.RefreshCommand.CanExecute(null))
                vm.RefreshCommand.Execute(null);
        }
    }

    private async System.Threading.Tasks.Task RunOperationAsync(
        string title,
        Func<System.Threading.CancellationToken,
             System.Collections.Generic.IAsyncEnumerable<Allpaca.Models.ProgressLine>> work)
    {
        var win = new LogWindow();
        win.Show(this);
        await win.RunAsync(title, work);
    }

    private void OnInfoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => new InfoWindow().ShowDialog(this);
}
