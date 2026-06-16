using System;
using System.Collections.Generic;
using Allpaca.Chrome;
using Allpaca.Models;
using Allpaca.Services;
using Allpaca.ViewModels;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Allpaca.Views;

public partial class SearchWindow : ChromeWindow
{
    private readonly SearchWindowViewModel _vm;

    public SearchWindow(SearchWindowViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;
    }

    // Parameterloser ctor nur fuer den XAML-Loader (Preview), Liefert leere Source-Map.
    public SearchWindow()
        : this(new SearchWindowViewModel(new Dictionary<PackageSourceKind, IPackageSource>()))
    {
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.SearchCommand.CanExecute(null))
        {
            _vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
