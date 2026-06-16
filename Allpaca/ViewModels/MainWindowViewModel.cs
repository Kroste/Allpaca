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

    public ObservableCollection<PackageItemViewModel> Packages { get; } = new();
    public ObservableCollection<SourceFilterViewModel> Filters { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountSummary))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountSummary))]
    private int _visibleCount;

    [ObservableProperty] private PackageItemViewModel? _selected;

    public string CountSummary => $"{VisibleCount} / {TotalCount} Pakete";

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
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

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
        var active = Filters.Where(f => f.IsSelected).Select(f => f.Kind).ToHashSet();
        var q = SearchText.Trim();

        IEnumerable<PackageItemViewModel> view = _all.Where(p => active.Contains(p.Model.Source));

        if (q.Length > 0)
            view = view.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(q, StringComparison.OrdinalIgnoreCase));

        var ordered = view
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Packages.Clear();
        foreach (var p in ordered)
            Packages.Add(p);

        VisibleCount = ordered.Count;
    }
}
