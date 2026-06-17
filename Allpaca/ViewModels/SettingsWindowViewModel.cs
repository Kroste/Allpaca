using Allpaca.Services.Ai;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;

namespace Allpaca.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public IReadOnlyList<AiProvider> Providers { get; } = new[]
    {
        AiProvider.Ollama, AiProvider.OpenAi, AiProvider.Anthropic, AiProvider.Gemini,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EndpointPlaceholder))]
    [NotifyPropertyChangedFor(nameof(ModelPlaceholder))]
    [NotifyPropertyChangedFor(nameof(NeedsApiKey))]
    private AiProvider _selectedProvider;

    [ObservableProperty] private string _endpointText = "";
    [ObservableProperty] private string _modelText = "";
    [ObservableProperty] private string _apiKeyText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestBrush))]
    private string _testStatus = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    public string EndpointPlaceholder => $"Default: {AiDefaults.Endpoint(SelectedProvider)}";
    public string ModelPlaceholder => $"Default: {AiDefaults.Model(SelectedProvider)}";
    public bool NeedsApiKey => SelectedProvider != AiProvider.Ollama;

    /// <summary>Grün = OK, rot = Fehler, sonst Default-Grau.</summary>
    public string TestBrush => TestStatus.StartsWith("✓") ? "#2BB673"
        : TestStatus.StartsWith("✗") ? "#E25555"
        : "#9AA0A8";

    /// <summary>Wird vom Code-Behind beim Klick auf Speichern aufgerufen, damit das
    /// MainWindow die neuen Einstellungen anwendet + persistiert.</summary>
    public event Action<AiSettings>? Saved;

    /// <summary>Wird vom Code-Behind als Fenster-zu-Lebenszyklus genutzt.</summary>
    public event Action? CloseRequested;

    public SettingsWindowViewModel(AiSettings current)
    {
        // Initial-Belegung aus aktuellen Settings - leere Strings statt null fuer TextBox-Bindings.
        _selectedProvider = current.Provider;
        _endpointText = current.Endpoint ?? "";
        _modelText = current.Model ?? "";
        _apiKeyText = current.ApiKey ?? "";
    }

    private AiSettings BuildSettings() => new()
    {
        Provider = SelectedProvider,
        Endpoint = string.IsNullOrWhiteSpace(EndpointText) ? null : EndpointText.Trim(),
        Model = string.IsNullOrWhiteSpace(ModelText) ? null : ModelText.Trim(),
        ApiKey = string.IsNullOrWhiteSpace(ApiKeyText) ? null : ApiKeyText,
    };

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        TestStatus = "Teste …";
        try
        {
            var settings = BuildSettings();
            var assistant = AiAssistantFactory.Create(settings);
            if (!assistant.IsConfigured)
            {
                TestStatus = "✗ Konfiguration unvollständig (API-Key fehlt?)";
                return;
            }

            var reply = await assistant.CompleteAsync(
                system: "Antworte ausschließlich mit dem Wort pong.",
                user: "ping",
                ct: new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

            var hit = reply?.IndexOf("pong", StringComparison.OrdinalIgnoreCase) >= 0;
            TestStatus = hit
                ? $"✓ Verbindung steht ({settings.ResolvedModel} via {settings.Provider})"
                : $"✓ Antwort erhalten – aber Inhalt unerwartet: {Truncate(reply, 60)}";
        }
        catch (TaskCanceledException)
        {
            TestStatus = "✗ Timeout – Provider antwortet nicht.";
        }
        catch (Exception ex)
        {
            TestStatus = $"✗ Fehler: {ex.Message}";
            Log.Warn(ex, "AI Test-Verbindung fehlgeschlagen ({0})", SelectedProvider);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Save()
    {
        Saved?.Invoke(BuildSettings());
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    private bool CanRun() => !IsBusy;

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";
}
