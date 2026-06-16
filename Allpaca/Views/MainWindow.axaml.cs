using System;
using System.Collections.Generic;
using System.Linq;
using Allpaca.Chrome;
using Allpaca.ViewModels;
using Avalonia.Controls;

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
            vm.OpenContainerInspector ??= name =>
            {
                var win = new ContainerInspectorWindow(vm.ProbeContainerPackagesAsync, name);
                win.Show(this);
            };

            if (vm.RefreshCommand.CanExecute(null))
                vm.RefreshCommand.Execute(null);
        }
    }

    private async System.Threading.Tasks.Task RunOperationAsync(
        OperationContext ctx,
        Func<System.Threading.CancellationToken,
             System.Collections.Generic.IAsyncEnumerable<Allpaca.Models.ProgressLine>> work)
    {
        var win = new LogWindow();
        win.Show(this);
        await win.RunAsync(ctx, work);
    }

    private void OnInfoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => new InfoWindow().ShowDialog(this);

    private void OnPackagesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // ListBox.SelectedItems ist IList und nicht direkt bindbar - wir spiegeln
        // den aktuellen Zustand auf das ViewModel, damit Batch-Buttons + Banner
        // rechtzeitig aktualisieren.
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is not ListBox lb) return;

        var items = lb.SelectedItems is { } sel
            ? sel.Cast<PackageItemViewModel>().ToList()
            : new List<PackageItemViewModel>();
        vm.SelectedItems = items;
    }
}
