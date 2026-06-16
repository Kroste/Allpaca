using System.Collections.ObjectModel;
using Allpaca.Models;
using Allpaca.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;

namespace Allpaca.ViewModels;

public partial class SearchWindowViewModel : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly IReadOnlyDictionary<PackageSourceKind, IPackageSource> _sources;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<PackageInfo> Results { get; } = new();

    [ObservableProperty] private string _query = "";

    [ObservableProperty] private bool _includeFlatpak = true;
    [ObservableProperty] private bool _includeHomebrew = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _errorText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _resultCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private PackageInfo? _selectedResult;

    public string StatusText => ErrorText is { Length: > 0 } e
        ? e
        : IsSearching
            ? "Suche läuft …"
            : ResultCount == 0
                ? "Tippe ein Stichwort und drücke „Suchen“."
                : $"{ResultCount} Treffer";

    /// <summary>Wird von der View beim Start gesetzt: oeffnet das LogWindow.</summary>
    public Func<OperationContext, Func<CancellationToken, IAsyncEnumerable<ProgressLine>>, Task>? RunOperation { get; set; }

    /// <summary>Wird von der View beim Start gesetzt: modaler Bestaetigungsdialog.</summary>
    public Func<ConfirmRequest, Task<bool>>? ConfirmAsync { get; set; }

    /// <summary>Callback nach erfolgreicher Installation - die MainWindow nutzt das,
    /// um seine Liste zu refreshen.</summary>
    public Action? AfterInstall { get; set; }

    public SearchWindowViewModel(IReadOnlyDictionary<PackageSourceKind, IPackageSource> sources)
    {
        _sources = sources;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            Results.Clear();
            ResultCount = 0;
            ErrorText = null;
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        Results.Clear();
        ResultCount = 0;
        ErrorText = null;
        IsSearching = true;

        try
        {
            var tasks = new List<Task<IReadOnlyList<PackageInfo>>>();
            if (IncludeFlatpak && _sources.TryGetValue(PackageSourceKind.Flatpak, out var fp))
                tasks.Add(SafeSearchAsync(fp, Query, ct));
            if (IncludeHomebrew && _sources.TryGetValue(PackageSourceKind.Homebrew, out var br))
                tasks.Add(SafeSearchAsync(br, Query, ct));

            if (tasks.Count == 0)
            {
                ErrorText = "Keine Quelle ausgewählt.";
                return;
            }

            var allResults = await Task.WhenAll(tasks);
            if (ct.IsCancellationRequested) return;

            // Zusammenfuehren, sortieren (Source-asc, dann Name-asc).
            var merged = allResults
                .SelectMany(r => r)
                .OrderBy(p => p.Source.ToString(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var p in merged) Results.Add(p);
            ResultCount = merged.Count;
            Log.Info("Suche '{0}': {1} Treffer", Query, merged.Count);
        }
        catch (OperationCanceledException) { /* neuer Search */ }
        catch (Exception ex)
        {
            Log.Error(ex, "Suche fehlgeschlagen");
            ErrorText = $"Fehler bei der Suche: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private static async Task<IReadOnlyList<PackageInfo>> SafeSearchAsync(
        IPackageSource src, string query, CancellationToken ct)
    {
        try { return await src.SearchAsync(query, ct); }
        catch (Exception ex)
        {
            Log.Warn(ex, "SearchAsync '{0}' fuer Quelle {1}", query, src.DisplayName);
            return Array.Empty<PackageInfo>();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (SelectedResult is null || RunOperation is null) return;
        if (!_sources.TryGetValue(SelectedResult.Source, out var src)) return;

        var name = SelectedResult.Name;
        var id = SelectedResult.Id;

        if (ConfirmAsync is not null)
        {
            var ok = await ConfirmAsync(new ConfirmRequest(
                Title: "Installieren?",
                Message: $"„{name}\" aus {src.DisplayName} installieren?",
                ConfirmLabel: "Installieren",
                IsDestructive: false));
            if (!ok)
            {
                Log.Info("Install abgebrochen vom User: {0}", id);
                return;
            }
        }

        Log.Info("Starte Install: {0} ({1})", id, src.DisplayName);
        await RunOperation(
            new OperationContext($"{src.DisplayName}: {name} installieren", src.Capabilities.RequiresReboot),
            ct => src.InstallAsync(id, ct));

        AfterInstall?.Invoke();
    }

    private bool CanInstall() => SelectedResult is not null;
}
