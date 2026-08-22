using System.Threading.Tasks;
using Allpaca.Chrome;
using Allpaca.Services.Ai;
using Allpaca.ViewModels;
using Avalonia.Interactivity;

namespace Allpaca.Views;

public partial class SettingsWindow : ChromeWindow
{
    private readonly SettingsWindowViewModel _vm;
    private readonly OllamaModelService _ollama = new();

    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;
        _vm.CloseRequested += Close;
        _vm.PullModelAsync = PullAsync;
    }

    // Parameterloser ctor nur für den XAML-Loader (Preview), default-Settings.
    public SettingsWindow() : this(new SettingsWindowViewModel(new AppPreferences(new AiSettings(), 0)))
    {
    }

    private async Task<bool> PullAsync(string model)
    {
        var endpoint = string.IsNullOrWhiteSpace(_vm.EndpointText)
            ? AiDefaults.Endpoint(AiProvider.Ollama)
            : _vm.EndpointText.Trim();

        var win = new OllamaPullWindow();
        win.Show(this);
        await win.PullAsync(_ollama, endpoint, model);

        // win.DataContext ist die OllamaPullViewModel - State prüfen.
        if (win.DataContext is OllamaPullViewModel pvm)
            return pvm.State == OperationState.Succeeded;
        return false;
    }
}
