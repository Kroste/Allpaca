using System.Collections.ObjectModel;
using Allpaca.Models;
using Allpaca.Services;
using Allpaca.Services.Ai;
using Allpaca.Services.Sources;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;

namespace Allpaca.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly PackageAggregator _aggregator;
    private readonly List<PackageItemViewModel> _all = new();
    private readonly Dictionary<PackageSourceKind, IPackageSource> _sourceByKind;
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private CancellationTokenSource? _updatesCheckCts;
    private bool _settingsReady;

    // Anti-Spam: Notifications nur, wenn sich die Update-Zahl seit dem letzten
    // Hinweis aendert. -1 = noch nie benachrichtigt.
    private int _lastNotifiedUpdateCount = -1;
    private bool _lastOsUpdateNotified;

    /// <summary>Wird von der View beim Start gesetzt: oeffnet das LogWindow und faedelt
    /// die Stream-Operation hindurch. ViewModel kennt damit weiterhin keine View-Typen.</summary>
    public Func<OperationContext, Func<CancellationToken, IAsyncEnumerable<ProgressLine>>, Task>? RunOperation { get; set; }

    /// <summary>Wird von der View beim Start gesetzt: zeigt einen modalen Bestaetigungsdialog
    /// und liefert true bei Bestaetigung, false bei Abbruch.</summary>
    public Func<ConfirmRequest, Task<bool>>? ConfirmAsync { get; set; }

    /// <summary>Wird von der View beim Start gesetzt: oeffnet ein Container-Inspector-Fenster
    /// fuer den angegebenen Distrobox-Container.</summary>
    public Action<string>? OpenContainerInspector { get; set; }

    /// <summary>Wird von der View beim Start gesetzt: oeffnet das SearchWindow fuer den
    /// Install-Flow. Die View baut die SearchWindowViewModel selbst, weil sie zusaetzlich
    /// LogWindow + Confirm-Dialog faedeln muss.</summary>
    public Action? OpenInstallSearch { get; set; }

    /// <summary>Wird von der View gesetzt: oeffnet das Settings-Fenster und kommt mit
    /// den geaenderten AppPreferences zurueck (oder null bei Abbruch).</summary>
    public Func<AppPreferences, Task<AppPreferences?>>? OpenSettings { get; set; }

    /// <summary>Wird von der View gesetzt: oeffnet das Aufraeum-Analyse-Fenster mit
    /// einem Snapshot der aktuellen Paketliste.</summary>
    public Action<IReadOnlyList<PackageItemViewModel>>? OpenCleanupAnalysis { get; set; }

    /// <summary>Aktuelle KI-Konfiguration. Provider/Endpoint/Modell werden persistiert,
    /// ApiKey lebt absichtlich nur im Speicher (siehe AppSettings-Kommentar).</summary>
    public AiSettings CurrentAi { get; private set; } = new();

    /// <summary>Aktuelles Auto-Refresh-Intervall in Minuten (0 = aus). Persistiert in
    /// settings.json. Treibt den DispatcherTimer in StartAutoRefresh.</summary>
    public int CurrentAutoRefreshMinutes { get; private set; }

    private DispatcherTimer? _autoRefreshTimer;

    /// <summary>Internal lookup, damit das MainWindow code-behind die SearchWindowViewModel
    /// mit der richtigen Quellen-Map fuettern kann.</summary>
    internal IReadOnlyDictionary<PackageSourceKind, IPackageSource> SourcesByKind => _sourceByKind;

    /// <summary>Liest die installierten Pakete *innerhalb* eines Distrobox-Containers.
    /// Wird vom ContainerInspectorWindow als Probe-Callback weitergereicht.</summary>
    public Task<IReadOnlyList<ContainerPackage>> ProbeContainerPackagesAsync(
        string containerName, CancellationToken ct = default)
    {
        if (!_sourceByKind.TryGetValue(PackageSourceKind.Distrobox, out var src))
            return Task.FromResult<IReadOnlyList<ContainerPackage>>(Array.Empty<ContainerPackage>());
        return ((DistroboxSource)src).ListContainerPackagesAsync(containerName, ct);
    }

    public ObservableCollection<PackageItemViewModel> Packages { get; } = new();
    public ObservableCollection<SourceFilterViewModel> Filters { get; } = new();

    public IReadOnlyList<SortOption> SortOptions { get; } = new[]
    {
        new SortOption(SortKey.Name, "Name"),
        new SortOption(SortKey.Size, "Größe"),
        new SortOption(SortKey.Source, "Quelle"),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountSummary))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountSummary))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private int _visibleCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowContainerPackagesCommand))]
    private PackageItemViewModel? _selected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCount))]
    [NotifyPropertyChangedFor(nameof(HasMultiSelection))]
    [NotifyPropertyChangedFor(nameof(CanBatchOperate))]
    [NotifyPropertyChangedFor(nameof(CanBatchUpdate))]
    [NotifyPropertyChangedFor(nameof(CanBatchUninstall))]
    [NotifyPropertyChangedFor(nameof(BatchInfo))]
    [NotifyCanExecuteChangedFor(nameof(BatchUpdateSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchUninstallSelectedCommand))]
    private IReadOnlyList<PackageItemViewModel> _selectedItems = Array.Empty<PackageItemViewModel>();

    public int SelectedCount => SelectedItems.Count;
    public bool HasMultiSelection => SelectedCount > 1;

    /// <summary>Batch geht, wenn alle Markierten aus derselben (batch-faehigen) Quelle
    /// kommen. CanBatchUpdate/Uninstall verfeinern das pro Operation anhand der
    /// Source-Capabilities (AppImage z. B. kann uninstall, aber kein update).</summary>
    public bool CanBatchOperate
    {
        get
        {
            if (!HasMultiSelection) return false;
            var first = SelectedItems[0].Model.Source;
            if (!SupportsBatch(first)) return false;
            return SelectedItems.All(p => p.Model.Source == first);
        }
    }

    public bool CanBatchUpdate => CanBatchOperate
        && _sourceByKind.TryGetValue(SelectedItems[0].Model.Source, out var src)
        && src.Capabilities.CanUpdate;

    public bool CanBatchUninstall => CanBatchOperate
        && _sourceByKind.TryGetValue(SelectedItems[0].Model.Source, out var src)
        && src.Capabilities.CanUninstall;

    public string BatchInfo
    {
        get
        {
            if (!HasMultiSelection) return "";
            if (CanBatchOperate) return $"{SelectedCount} Pakete markiert";
            return $"{SelectedCount} Pakete markiert – Batch-Aktionen brauchen gleiche Quelle (Flatpak, Homebrew, AppImage oder pipx)";
        }
    }

    [ObservableProperty] private SortOption _selectedSortOption = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortDirectionGlyph))]
    private bool _sortDescending;

    [ObservableProperty] private bool _showRuntimes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private bool _showUpdatesOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOsUpdate))]
    [NotifyCanExecuteChangedFor(nameof(ApplyOsUpdateCommand))]
    private string? _osUpdateMessage;

    public bool HasOsUpdate => !string.IsNullOrEmpty(OsUpdateMessage);

    public string CountSummary => $"{VisibleCount} / {TotalCount} Pakete";
    public string SortDirectionGlyph => SortDescending ? "▼" : "▲";

    /// <summary>Sichtbar, sobald die Trefferliste leer ist - signalisiert dem User,
    /// warum gerade nichts da ist (Loading, gar nichts installiert, alle gefiltert,
    /// Suche ohne Treffer). Waehrend Loading laeuft der ProgressBar parallel oben.</summary>
    public bool ShowEmptyState => VisibleCount == 0;

    public string EmptyStateText
    {
        get
        {
            if (IsLoading) return "Pakete werden geladen …";
            if (TotalCount == 0)
                return "Keine Pakete gefunden. Sind flatpak, brew & Co. installiert und im PATH?";
            // Es gibt Eintraege, aber keiner ist gerade sichtbar.
            var q = SearchText.Trim();
            if (q.Length > 0)
                return $"Keine Treffer für „{q}“. Versuche einen anderen Suchbegriff oder erweitere die Quellen-Auswahl links.";
            if (ShowUpdatesOnly)
                return IsCheckingUpdates
                    ? "Update-Check läuft – die aktualisierbaren Pakete erscheinen gleich."
                    : "Keine aktualisierbaren Pakete. Deaktiviere „Nur aktualisierbare anzeigen“ links, um wieder die volle Liste zu sehen.";
            return "Keine Einträge sichtbar – aktive Quellen-Filter oder „Runtimes anzeigen“ prüfen.";
        }
    }

    public MainWindowViewModel() : this(new SettingsService()) { }

    public MainWindowViewModel(SettingsService settingsService)
    {
        _settings = settingsService;

        var runner = new ProcessRunner(new SandboxDetector());
        _notifications = new NotificationService(runner);
        var sources = new IPackageSource[]
        {
            new FlatpakSource(runner),
            new HomebrewSource(runner),
            new RpmOstreeSource(runner),
            new DistroboxSource(runner),
            new AppImageSource(),
            new PipxSource(runner),
        };
        _aggregator = new PackageAggregator(sources);
        _sourceByKind = sources.ToDictionary(s => s.Kind);

        foreach (var s in sources)
        {
            var f = new SourceFilterViewModel(s.Kind, s.DisplayName, PackageItemViewModel.ColorFor(s.Kind));
            f.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SourceFilterViewModel.IsSelected))
                {
                    ApplyFilter();
                    SaveSettings();
                }
            };
            Filters.Add(f);
        }

        SelectedSortOption = SortOptions[0];

        // Settings aus dem User-Config-Pfad laden + anwenden. Wahrend Apply duerfen die
        // partial-Method-Handler NICHT speichern, sonst speichern wir die geladenen
        // Werte direkt wieder zurueck (egal, aber unsauber).
        ApplySettings(_settings.Load());
        _settingsReady = true;
    }

    private void ApplySettings(AppSettings s)
    {
        ShowRuntimes = s.ShowRuntimes;
        ShowUpdatesOnly = s.ShowUpdatesOnly;
        SortDescending = s.SortDescending;

        if (Enum.TryParse<SortKey>(s.SortKey, ignoreCase: true, out var key))
        {
            var opt = SortOptions.FirstOrDefault(o => o.Key == key);
            if (opt is not null) SelectedSortOption = opt;
        }

        foreach (var f in Filters)
        {
            if (s.SourceFilters.TryGetValue(f.Kind.ToString(), out var enabled))
                f.IsSelected = enabled;
        }

        // KI: Enum-Parse mit Fallback auf Ollama (Default), Endpoint/Modell nullbar.
        var provider = Enum.TryParse<AiProvider>(s.AiProvider, ignoreCase: true, out var p)
            ? p : AiProvider.Ollama;
        CurrentAi = new AiSettings
        {
            Provider = provider,
            Endpoint = string.IsNullOrWhiteSpace(s.AiEndpoint) ? null : s.AiEndpoint,
            Model = string.IsNullOrWhiteSpace(s.AiModel) ? null : s.AiModel,
            // ApiKey nicht aus Settings - in-memory only.
        };

        CurrentAutoRefreshMinutes = Math.Max(0, s.AutoRefreshMinutes);
        ApplyAutoRefresh();
    }

    private void SaveSettings()
    {
        if (!_settingsReady) return;
        _settings.Save(new AppSettings
        {
            SortKey = SelectedSortOption?.Key.ToString() ?? nameof(SortKey.Name),
            SortDescending = SortDescending,
            ShowRuntimes = ShowRuntimes,
            ShowUpdatesOnly = ShowUpdatesOnly,
            SourceFilters = Filters.ToDictionary(f => f.Kind.ToString(), f => f.IsSelected),
            AiProvider = CurrentAi.Provider.ToString(),
            AiEndpoint = CurrentAi.Endpoint,
            AiModel = CurrentAi.Model,
            AutoRefreshMinutes = CurrentAutoRefreshMinutes,
        });
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(SortOption value) { ApplyFilter(); SaveSettings(); }
    partial void OnSortDescendingChanged(bool value) { ApplyFilter(); SaveSettings(); }
    partial void OnShowRuntimesChanged(bool value) { ApplyFilter(); SaveSettings(); }
    partial void OnShowUpdatesOnlyChanged(bool value) { ApplyFilter(); SaveSettings(); }

    [RelayCommand]
    private void ToggleSortDirection() => SortDescending = !SortDescending;

    /// <summary>Alle Mutations-faehigen Quellen sind verdrahtet: Flatpak, rpm-ostree
    /// (pkexec, Reboot-Hinweis), Homebrew, Distrobox (Container loeschen/upgraden),
    /// AppImage (Datei + Waisen-.desktop loeschen) und pipx (Python-CLI-Tools).</summary>
    private bool IsWiredForMutation(PackageItemViewModel? item) => item?.Model.Source switch
    {
        PackageSourceKind.Flatpak => true,
        PackageSourceKind.RpmOstree => true,
        PackageSourceKind.Homebrew => true,
        PackageSourceKind.Distrobox => true,
        PackageSourceKind.AppImage => true,
        PackageSourceKind.Pipx => true,
        _ => false,
    };

    /// <summary>Batch-faehige Quellen. Flatpak/Homebrew haben natives Multi-ID-CLI,
    /// AppImage und pipx iterieren ueber die Default-Implementierung von
    /// UninstallManyAsync - schnell genug fuer Dutzende Eintraege.</summary>
    private static bool SupportsBatch(PackageSourceKind kind) => kind switch
    {
        PackageSourceKind.Flatpak => true,
        PackageSourceKind.Homebrew => true,
        PackageSourceKind.AppImage => true,
        PackageSourceKind.Pipx => true,
        _ => false,
    };

    [RelayCommand(CanExecute = nameof(CanUninstallSelected))]
    private async Task UninstallSelectedAsync()
    {
        if (Selected is null || RunOperation is null) return;
        if (!_sourceByKind.TryGetValue(Selected.Model.Source, out var src)) return;

        var id = Selected.Model.Id;
        var name = Selected.Name;

        // Destruktive Aktion - vor dem Start nachfragen, sofern die View einen
        // Dialog eingehaengt hat (Headless-Tests koennen das weglassen).
        if (ConfirmAsync is not null)
        {
            var ok = await ConfirmAsync(BuildUninstallConfirmRequest(src, name));
            if (!ok)
            {
                Log.Info("Uninstall abgebrochen vom User: {0}", id);
                return;
            }
        }

        var title = $"{src.DisplayName}: {name} deinstallieren";
        Log.Info("Starte Uninstall: {0} ({1})", id, src.DisplayName);

        await RunOperation(
            new OperationContext(title, src.Capabilities.RequiresReboot),
            ct => src.UninstallAsync(id, ct));

        // Liste nach der Operation neu laden, der Eintrag sollte verschwunden sein.
        await RefreshAsync();
    }

    private bool CanUninstallSelected() => IsWiredForMutation(Selected);

    /// <summary>Quellen-spezifischer Confirm-Text. Distrobox ist ein Sonderfall - das
    /// "Uninstall" loescht den GESAMTEN Container inklusive aller Daten, deshalb
    /// muss der Dialog das laut und deutlich sagen.</summary>
    private static ConfirmRequest BuildUninstallConfirmRequest(IPackageSource src, string name)
    {
        if (src.Kind == PackageSourceKind.Distrobox)
        {
            return new ConfirmRequest(
                Title: "Container löschen?",
                Message: $"Den Distrobox-Container „{name}\" wirklich komplett löschen? Alle darin installierten Pakete und enthaltenen Daten gehen unwiederbringlich verloren. Dieser Schritt lässt sich NICHT rückgängig machen.",
                ConfirmLabel: "Container löschen",
                IsDestructive: true);
        }

        var msg = new System.Text.StringBuilder();
        msg.Append($"„{name}\" wirklich aus {src.DisplayName} entfernen?");
        if (src.Capabilities.RequiresReboot)
            msg.Append(" Die Änderung wird erst nach einem Neustart vollständig wirksam.");
        msg.Append(" Dieser Schritt lässt sich nur durch erneutes Installieren rückgängig machen.");

        return new ConfirmRequest(
            Title: "Deinstallieren?",
            Message: msg.ToString(),
            ConfirmLabel: "Deinstallieren",
            IsDestructive: true);
    }

    [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
    private async Task UpdateSelectedAsync()
    {
        if (Selected is null || RunOperation is null) return;
        if (!_sourceByKind.TryGetValue(Selected.Model.Source, out var src)) return;

        var id = Selected.Model.Id;
        var name = Selected.Name;
        var title = $"{src.DisplayName}: {name} aktualisieren";
        Log.Info("Starte Update: {0} ({1})", id, src.DisplayName);

        await RunOperation(
            new OperationContext(title, src.Capabilities.RequiresReboot),
            ct => src.UpdateAsync(id, ct));

        await RefreshAsync();
    }

    private bool CanUpdateSelected() => IsWiredForMutation(Selected);

    [RelayCommand(CanExecute = nameof(CanShowContainerPackages))]
    private void ShowContainerPackages()
    {
        if (Selected is null || OpenContainerInspector is null) return;
        if (Selected.Model.Source != PackageSourceKind.Distrobox) return;
        OpenContainerInspector(Selected.Model.Id);
    }

    private bool CanShowContainerPackages() =>
        Selected?.Model.Source == PackageSourceKind.Distrobox;

    [RelayCommand(CanExecute = nameof(CanApplyOsUpdate))]
    private async Task ApplyOsUpdateAsync()
    {
        if (RunOperation is null) return;
        if (!_sourceByKind.TryGetValue(PackageSourceKind.RpmOstree, out var src)) return;

        Log.Info("Starte rpm-ostree upgrade: {0}", OsUpdateMessage);
        await RunOperation(
            new OperationContext("rpm-ostree: System aktualisieren", RequiresReboot: true),
            ct => src.UpdateAsync(null, ct));

        // Nach erfolgreicher Anwendung sollte der Banner verschwinden - der naechste
        // Refresh laeuft sowieso, der bringt einen frischen Check mit.
        OsUpdateMessage = null;
        await RefreshAsync();
    }

    private bool CanApplyOsUpdate() => HasOsUpdate;

    [RelayCommand]
    private void DismissOsUpdate() => OsUpdateMessage = null;

    [RelayCommand]
    private void OpenInstall() => OpenInstallSearch?.Invoke();

    [RelayCommand]
    private void OpenCleanup()
    {
        // Snapshot kopieren - die Analyse soll auf dem aktuellen Stand laufen,
        // auch wenn die Hauptliste danach refreshed wird.
        OpenCleanupAnalysis?.Invoke(_all.ToList());
    }

    [RelayCommand]
    private async Task OpenAiSettingsAsync()
    {
        if (OpenSettings is null) return;
        var updated = await OpenSettings(new AppPreferences(CurrentAi, CurrentAutoRefreshMinutes));
        if (updated is null) return;  // Abbruch

        CurrentAi = updated.Ai;
        if (updated.AutoRefreshMinutes != CurrentAutoRefreshMinutes)
        {
            CurrentAutoRefreshMinutes = updated.AutoRefreshMinutes;
            ApplyAutoRefresh();
        }
        SaveSettings();
        Log.Info("Settings aktualisiert: Provider={0}, Modell={1}, AutoRefresh={2}min",
            CurrentAi.Provider, CurrentAi.ResolvedModel, CurrentAutoRefreshMinutes);
    }

    private void ApplyAutoRefresh()
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer = null;

        if (CurrentAutoRefreshMinutes <= 0) return;

        _autoRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(CurrentAutoRefreshMinutes),
        };
        _autoRefreshTimer.Tick += (_, _) =>
        {
            // Nicht parallel starten, wenn schon was laeuft.
            if (!IsLoading && RefreshCommand.CanExecute(null))
                RefreshCommand.Execute(null);
        };
        _autoRefreshTimer.Start();
        Log.Info("Auto-Refresh aktiv: alle {0} Minuten", CurrentAutoRefreshMinutes);
    }

    [RelayCommand(CanExecute = nameof(CanBatchUpdate))]
    private async Task BatchUpdateSelectedAsync()
    {
        if (RunOperation is null) return;
        var items = SelectedItems.ToList();
        if (items.Count == 0) return;

        // CanBatchOperate hat schon zugesichert: alle aus derselben (batch-faehigen) Quelle.
        var srcKind = items[0].Model.Source;
        if (!_sourceByKind.TryGetValue(srcKind, out var src)) return;

        var ids = items.Select(p => p.Model.Id).ToList();
        var title = $"{src.DisplayName}: {items.Count} Pakete aktualisieren";
        Log.Info("Starte Batch-Update via {0}: {1} Pakete", src.DisplayName, items.Count);

        await RunOperation(
            new OperationContext(title, src.Capabilities.RequiresReboot),
            ct => src.UpdateManyAsync(ids, ct));

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanBatchUninstall))]
    private async Task BatchUninstallSelectedAsync()
    {
        if (RunOperation is null) return;
        var items = SelectedItems.ToList();
        if (items.Count == 0) return;

        var srcKind = items[0].Model.Source;
        if (!_sourceByKind.TryGetValue(srcKind, out var src)) return;

        if (ConfirmAsync is not null)
        {
            var bullets = string.Join("\n", items.Take(8).Select(p => $"• {p.Name}"));
            if (items.Count > 8) bullets += $"\n• … und {items.Count - 8} weitere";
            var ok = await ConfirmAsync(new ConfirmRequest(
                Title: $"{items.Count} Pakete deinstallieren?",
                Message: $"Folgende Einträge wirklich aus {src.DisplayName} entfernen?\n\n{bullets}\n\nDieser Schritt lässt sich nur durch erneutes Installieren rückgängig machen.",
                ConfirmLabel: "Alle deinstallieren",
                IsDestructive: true));
            if (!ok)
            {
                Log.Info("Batch-Uninstall abgebrochen vom User: {0} Pakete", items.Count);
                return;
            }
        }

        var ids = items.Select(p => p.Model.Id).ToList();
        var title = $"{src.DisplayName}: {items.Count} Pakete deinstallieren";
        Log.Info("Starte Batch-Uninstall via {0}: {1} Pakete", src.DisplayName, items.Count);

        await RunOperation(
            new OperationContext(title, src.Capabilities.RequiresReboot),
            ct => src.UninstallManyAsync(ids, ct));

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        _all.Clear();
        Packages.Clear();
        // UpdateCount mit zurueck auf 0 - sonst zeigt die Sidebar stale "↑N", waehrend
        // die neuen PackageItemViewModels noch alle HasUpdate=false haben.
        foreach (var f in Filters) { f.Count = 0; f.Status = null; f.UpdateCount = 0; }
        TotalCount = 0;
        VisibleCount = 0;

        try
        {
            // Alle Quellen parallel laden, Ergebnisse einlaufen lassen, sobald fertig.
            var tasks = _aggregator.Sources
                .Select(s => _aggregator.LoadOneAsync(s))
                .ToList();

            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks);
                tasks.Remove(done);

                var res = await done;
                var filter = Filters.First(f => f.Kind == res.Source.Kind);
                filter.IsAvailable = res.Available;
                filter.Status = res.Error;

                foreach (var p in res.Packages)
                    _all.Add(new PackageItemViewModel(p));

                filter.Count = res.Packages.Count;
                TotalCount = _all.Count;
                ApplyFilter();
            }
        }
        finally
        {
            IsLoading = false;
        }

        // Update-Check laeuft im Hintergrund - Liste ist sofort sichtbar, Badges fliegen
        // ein, sobald die Antworten der Quellen da sind. Bei erneutem Refresh canceln wir
        // den vorherigen Check, damit kein stale Ergebnis dazwischenfunkt.
        _updatesCheckCts?.Cancel();
        _updatesCheckCts?.Dispose();
        _updatesCheckCts = new CancellationTokenSource();
        _ = CheckUpdatesInBackgroundAsync(_updatesCheckCts.Token);
        _ = CheckOsUpdateInBackgroundAsync(_updatesCheckCts.Token);
    }

    private async Task CheckOsUpdateInBackgroundAsync(CancellationToken ct)
    {
        try
        {
            if (!_sourceByKind.TryGetValue(PackageSourceKind.RpmOstree, out var src))
            {
                OsUpdateMessage = null;
                return;
            }

            if (src is not RpmOstreeSource rpm) return;
            if (!await rpm.IsAvailableAsync(ct)) return;

            var info = await rpm.CheckOsUpdateAsync(ct);
            if (ct.IsCancellationRequested) return;

            OsUpdateMessage = info;
            Log.Info("rpm-ostree OS-Update: {0}", info ?? "keins");

            // Toast nur beim Uebergang nicht-vorhanden -> verfuegbar.
            var hasUpdate = !string.IsNullOrEmpty(info);
            if (hasUpdate && !_lastOsUpdateNotified)
            {
                _ = _notifications.NotifyAsync(
                    title: "rpm-ostree: OS-Update verfügbar",
                    body: $"Version {info} – Neustart erforderlich. Im Hauptfenster ist der Banner sichtbar.",
                    ct: ct);
            }
            _lastOsUpdateNotified = hasUpdate;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Warn(ex, "rpm-ostree OS-Update-Check fehlgeschlagen");
        }
    }

    private async Task CheckUpdatesInBackgroundAsync(CancellationToken ct)
    {
        IsCheckingUpdates = true;
        try
        {
            var tasks = _aggregator.Sources
                .Select(async s => (Source: s, Ids: await SafeCheckUpdatesAsync(s, ct)))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            if (ct.IsCancellationRequested) return;

            // Reset zuerst, dann anhand der Ergebnisse markieren.
            foreach (var p in _all) p.HasUpdate = false;

            foreach (var (src, ids) in results)
            {
                if (ids.Count == 0) continue;
                foreach (var p in _all)
                    if (p.Model.Source == src.Kind && ids.Contains(p.Model.Id))
                        p.HasUpdate = true;
            }

            var updateCount = _all.Count(p => p.HasUpdate);
            Log.Info("Update-Check fertig: {0} Updates verfuegbar", updateCount);

            // Per-Source-Counter nachziehen, damit das gruene "↑ N"-Badge in der
            // Sidebar passt.
            foreach (var f in Filters)
                f.UpdateCount = _all.Count(p => p.Model.Source == f.Kind && p.HasUpdate);

            // ApplyFilter rebuildet die Packages-Collection, dadurch wird die UI
            // neu gerendert und die HasUpdate-Badges erscheinen.
            ApplyFilter();

            // Toast nur bei Aenderung gegenueber dem letzten Lauf - sonst wuerde
            // jeder Refresh die gleichen N Updates erneut anzeigen.
            if (updateCount > 0 && updateCount != _lastNotifiedUpdateCount)
            {
                _ = _notifications.NotifyAsync(
                    title: $"{updateCount} Update{(updateCount == 1 ? "" : "s")} verfügbar",
                    body: "Öffne Allpaca, um die betroffenen Pakete zu aktualisieren.",
                    ct: ct);
            }
            _lastNotifiedUpdateCount = updateCount;
        }
        catch (OperationCanceledException)
        {
            // Neuer Refresh hat uns abgeschossen - alles gut.
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Update-Check fehlgeschlagen");
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private static async Task<IReadOnlySet<string>> SafeCheckUpdatesAsync(
        IPackageSource s, CancellationToken ct)
    {
        try
        {
            return await s.CheckUpdatesAsync(ct);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Update-Check Quelle '{0}'", s.DisplayName);
            return new HashSet<string>();
        }
    }

    private void ApplyFilter()
    {
        // Duplikat-Status ueber die volle Liste recompute - mit jedem zusaetzlich
        // geladenen Source-Result koennen neue Doppel auftauchen.
        PackageDuplicateDetector.Annotate(_all);

        var active = Filters.Where(f => f.IsSelected).Select(f => f.Kind).ToHashSet();
        var q = SearchText.Trim();

        IEnumerable<PackageItemViewModel> view = _all.Where(p => active.Contains(p.Model.Source));

        if (!ShowRuntimes)
            view = view.Where(p => !p.Model.IsRuntime);

        if (ShowUpdatesOnly)
            view = view.Where(p => p.HasUpdate);

        if (q.Length > 0)
            view = view.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(q, StringComparison.OrdinalIgnoreCase));

        var ordered = PackageSorter
            .Sort(view, SelectedSortOption?.Key ?? SortKey.Name, SortDescending)
            .ToList();

        Packages.Clear();
        foreach (var p in ordered)
            Packages.Add(p);

        VisibleCount = ordered.Count;
    }
}
