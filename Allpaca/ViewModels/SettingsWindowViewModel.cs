using System.Collections.ObjectModel;
using Allpaca.Services.Ai;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;

namespace Allpaca.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly OllamaModelService _ollamaModels;

    public IReadOnlyList<AiProvider> Providers { get; } = new[]
    {
        AiProvider.Ollama, AiProvider.OpenAi, AiProvider.Anthropic, AiProvider.Gemini,
    };

    /// <summary>Lokal installierte Ollama-Modelle, via /api/tags geladen. Leer für
    /// andere Provider.</summary>
    public ObservableCollection<string> LocalOllamaModels { get; } = new();

    public IReadOnlyList<OllamaCuratedModel> CuratedOllamaModels { get; } = OllamaCuratedModels.All;

    public IReadOnlyList<AutoRefreshPreset> AutoRefreshPresets { get; } = new[]
    {
        new AutoRefreshPreset(0,  "Aus"),
        new AutoRefreshPreset(5,  "alle 5 Min"),
        new AutoRefreshPreset(15, "alle 15 Min"),
        new AutoRefreshPreset(30, "alle 30 Min"),
        new AutoRefreshPreset(60, "alle 60 Min"),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EndpointPlaceholder))]
    [NotifyPropertyChangedFor(nameof(ModelPlaceholder))]
    [NotifyPropertyChangedFor(nameof(NeedsApiKey))]
    [NotifyPropertyChangedFor(nameof(IsOllama))]
    [NotifyCanExecuteChangedFor(nameof(LoadLocalModelsCommand))]
    private AiProvider _selectedProvider;

    [ObservableProperty] private string _endpointText = "";
    [ObservableProperty] private string _modelText = "";
    [ObservableProperty] private string _apiKeyText = "";

    [ObservableProperty] private string _modelsStatus = "";

    /// <summary>Bei Klick auf einen Eintrag in der Modell-Liste landet er als ModelText.</summary>
    [ObservableProperty] private string? _selectedLocalModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PullCuratedCommand))]
    private OllamaCuratedModel? _selectedCuratedModel;

    [ObservableProperty] private AutoRefreshPreset? _selectedAutoRefresh;

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
    public bool IsOllama => SelectedProvider == AiProvider.Ollama;

    /// <summary>Grün = OK, rot = Fehler, sonst Default-Grau.</summary>
    public string TestBrush => TestStatus.StartsWith("✓") ? "#2BB673"
        : TestStatus.StartsWith("✗") ? "#E25555"
        : "#9AA0A8";

    /// <summary>Wird vom Code-Behind beim Klick auf Speichern aufgerufen, damit das
    /// MainWindow die neuen Einstellungen anwendet + persistiert.</summary>
    public event Action<AppPreferences>? Saved;

    /// <summary>Wird vom Code-Behind als Fenster-zu-Lebenszyklus genutzt.</summary>
    public event Action? CloseRequested;

    /// <summary>Wird vom Code-Behind gesetzt: öffnet ein OllamaPullWindow für das
    /// übergebene Modell und liefert true zurück, wenn der Pull erfolgreich war.</summary>
    public Func<string, Task<bool>>? PullModelAsync { get; set; }

    public SettingsWindowViewModel(AppPreferences current)
        : this(current, new OllamaModelService()) { }

    public SettingsWindowViewModel(AppPreferences current, OllamaModelService ollamaModels)
    {
        // Initial-Belegung aus aktuellen Settings - leere Strings statt null für TextBox-Bindings.
        var ai = current.Ai;
        _selectedProvider = ai.Provider;
        _endpointText = ai.Endpoint ?? "";
        _modelText = ai.Model ?? "";
        _apiKeyText = ai.ApiKey ?? "";
        _ollamaModels = ollamaModels;

        // Auto-Refresh-Preset finden, das zum aktuellen Wert passt; Fallback "Aus".
        _selectedAutoRefresh = AutoRefreshPresets.FirstOrDefault(p => p.Minutes == current.AutoRefreshMinutes)
            ?? AutoRefreshPresets[0];
    }

    partial void OnSelectedLocalModelChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ModelText = value;
    }

    private AiSettings BuildAiSettings() => new()
    {
        Provider = SelectedProvider,
        Endpoint = string.IsNullOrWhiteSpace(EndpointText) ? null : EndpointText.Trim(),
        Model = string.IsNullOrWhiteSpace(ModelText) ? null : ModelText.Trim(),
        ApiKey = string.IsNullOrWhiteSpace(ApiKeyText) ? null : ApiKeyText,
    };

    private AppPreferences BuildPreferences() => new(
        Ai: BuildAiSettings(),
        AutoRefreshMinutes: SelectedAutoRefresh?.Minutes ?? 0);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        TestStatus = "Teste …";
        try
        {
            var settings = BuildAiSettings();
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
        Saved?.Invoke(BuildPreferences());
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanLoadModels))]
    private async Task LoadLocalModelsAsync()
    {
        IsBusy = true;
        ModelsStatus = "Lade …";
        LocalOllamaModels.Clear();
        try
        {
            var endpoint = string.IsNullOrWhiteSpace(EndpointText)
                ? AiDefaults.Endpoint(AiProvider.Ollama)
                : EndpointText.Trim();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var names = await _ollamaModels.ListLocalAsync(endpoint, cts.Token);
            foreach (var n in names) LocalOllamaModels.Add(n);
            ModelsStatus = names.Count == 0
                ? "Keine Modelle gefunden. „ollama pull“ läuft im Terminal."
                : $"{names.Count} Modelle gefunden – Eintrag klicken übernimmt ihn.";
        }
        catch (Exception ex)
        {
            ModelsStatus = $"Fehler: {ex.Message}";
            Log.Warn(ex, "Ollama /api/tags");
        }
        finally { IsBusy = false; }
    }

    private bool CanLoadModels() => !IsBusy && IsOllama;

    [RelayCommand(CanExecute = nameof(CanPullCurated))]
    private async Task PullCuratedAsync()
    {
        if (PullModelAsync is null || SelectedCuratedModel is null) return;

        var ok = await PullModelAsync(SelectedCuratedModel.Name);
        if (ok)
        {
            // Erfolgreich gezogen -> Modell direkt als aktives Modell übernehmen
            // und die lokale Liste neu laden, damit das neue Modell auftaucht.
            ModelText = SelectedCuratedModel.Name;
            await LoadLocalModelsAsync();
        }
    }

    private bool CanPullCurated() => IsOllama && SelectedCuratedModel is not null;

    private bool CanRun() => !IsBusy;

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";
}
