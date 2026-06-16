using System.Collections.ObjectModel;
using Allpaca.Models;
using Allpaca.Services;
using Allpaca.Services.Sources;
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

    /// <summary>Wird von der View beim Start gesetzt: oeffnet das LogWindow und faedelt
    /// die Stream-Operation hindurch. ViewModel kennt damit weiterhin keine View-Typen.</summary>
    public Func<OperationContext, Func<CancellationToken, IAsyncEnumerable<ProgressLine>>, Task>? RunOperation { get; set; }

    /// <summary>Wird von der View beim Start gesetzt: zeigt einen modalen Bestaetigungsdialog
    /// und liefert true bei Bestaetigung, false bei Abbruch.</summary>
    public Func<ConfirmRequest, Task<bool>>? ConfirmAsync { get; set; }

    /// <summary>Wird von der View beim Start gesetzt: oeffnet ein Container-Inspector-Fenster
    /// fuer den angegebenen Distrobox-Container.</summary>
    public Action<string>? OpenContainerInspector { get; set; }

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

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountSummary))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountSummary))]
    private int _visibleCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowContainerPackagesCommand))]
    private PackageItemViewModel? _selected;

    [ObservableProperty] private SortOption _selectedSortOption = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortDirectionGlyph))]
    private bool _sortDescending;

    [ObservableProperty] private bool _showRuntimes;

    public string CountSummary => $"{VisibleCount} / {TotalCount} Pakete";
    public string SortDirectionGlyph => SortDescending ? "▼" : "▲";

    public MainWindowViewModel()
    {
        var runner = new ProcessRunner(new SandboxDetector());
        var sources = new IPackageSource[]
        {
            new FlatpakSource(runner),
            new HomebrewSource(runner),
            new RpmOstreeSource(runner),
            new DistroboxSource(runner),
            new AppImageSource(),
        };
        _aggregator = new PackageAggregator(sources);
        _sourceByKind = sources.ToDictionary(s => s.Kind);

        foreach (var s in sources)
        {
            var f = new SourceFilterViewModel(s.Kind, s.DisplayName, PackageItemViewModel.ColorFor(s.Kind));
            f.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SourceFilterViewModel.IsSelected))
                    ApplyFilter();
            };
            Filters.Add(f);
        }

        SelectedSortOption = SortOptions[0];
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedSortOptionChanged(SortOption value) => ApplyFilter();
    partial void OnSortDescendingChanged(bool value) => ApplyFilter();
    partial void OnShowRuntimesChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private void ToggleSortDirection() => SortDescending = !SortDescending;

    /// <summary>Aktuell verdrahtet: Flatpak (rootless) und rpm-ostree (laeuft via
    /// pkexec, Reboot-Hinweis nach Erfolg). Distrobox bekommt einen eigenen
    /// Drill-down-Pfad, Homebrew/AppImage folgen in spaeteren Commits.</summary>
    private bool IsWiredForMutation(PackageItemViewModel? item) => item?.Model.Source switch
    {
        PackageSourceKind.Flatpak => true,
        PackageSourceKind.RpmOstree => true,
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
            var msg = new System.Text.StringBuilder();
            msg.Append($"„{name}\" wirklich aus {src.DisplayName} entfernen?");
            if (src.Capabilities.RequiresReboot)
                msg.Append(" Die Änderung wird erst nach einem Neustart vollständig wirksam.");
            msg.Append(" Dieser Schritt lässt sich nur durch erneutes Installieren rückgängig machen.");

            var ok = await ConfirmAsync(new ConfirmRequest(
                Title: "Deinstallieren?",
                Message: msg.ToString(),
                ConfirmLabel: "Deinstallieren",
                IsDestructive: true));
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

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        _all.Clear();
        Packages.Clear();
        foreach (var f in Filters) { f.Count = 0; f.Status = null; }
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
