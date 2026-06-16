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
    public Func<string, Func<CancellationToken, IAsyncEnumerable<ProgressLine>>, Task>? RunOperation { get; set; }

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

    /// <summary>v2 erster Schritt: Uninstall ist nur fuer Flatpak verdrahtet. Andere
    /// Quellen brauchen pkexec (rpm-ostree), Drill-down (Distrobox) oder eigene Hooks
    /// (Homebrew/AppImage) und folgen in spaeteren Commits.</summary>
    private bool IsWiredForMutation(PackageItemViewModel? item) =>
        item is not null && item.Model.Source == PackageSourceKind.Flatpak;

    [RelayCommand(CanExecute = nameof(CanUninstallSelected))]
    private async Task UninstallSelectedAsync()
    {
        if (Selected is null || RunOperation is null) return;
        if (!_sourceByKind.TryGetValue(Selected.Model.Source, out var src)) return;

        var id = Selected.Model.Id;
        var name = Selected.Name;
        var title = $"{src.DisplayName}: {name} deinstallieren";
        Log.Info("Starte Uninstall: {0} ({1})", id, src.DisplayName);

        await RunOperation(title, ct => src.UninstallAsync(id, ct));

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

        await RunOperation(title, ct => src.UpdateAsync(id, ct));

        await RefreshAsync();
    }

    private bool CanUpdateSelected() => IsWiredForMutation(Selected);

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
