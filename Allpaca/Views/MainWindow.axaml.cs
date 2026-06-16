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

        if (DataContext is MainWindowViewModel vm && vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);
    }

    private void OnInfoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => new InfoWindow().ShowDialog(this);
}
