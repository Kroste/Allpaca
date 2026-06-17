using Allpaca.Chrome;
using Allpaca.Services.Ai;
using Allpaca.ViewModels;
using Avalonia.Interactivity;

namespace Allpaca.Views;

public partial class SettingsWindow : ChromeWindow
{
    private readonly SettingsWindowViewModel _vm;

    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;
        _vm.CloseRequested += Close;
    }

    // Parameterloser ctor nur fuer den XAML-Loader (Preview), default-Settings.
    public SettingsWindow() : this(new SettingsWindowViewModel(new AiSettings()))
    {
    }
}
