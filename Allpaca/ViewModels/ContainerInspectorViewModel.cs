using System.Collections.ObjectModel;
using Allpaca.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Allpaca.ViewModels;

public partial class ContainerInspectorViewModel : ObservableObject
{
    public ObservableCollection<ContainerPackage> Packages { get; } = new();
    private readonly List<ContainerPackage> _all = new();

    [ObservableProperty] private string _containerName = "";

    [ObservableProperty] private string _title = "Pakete im Container";

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _visibleCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string StatusText => ErrorMessage is { Length: > 0 } e
        ? e
        : IsLoading
            ? "Lade …"
            : $"{VisibleCount} / {TotalCount} Pakete";

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(StatusText));

    internal void SetResult(IReadOnlyList<ContainerPackage> result)
    {
        ErrorMessage = null;
        _all.Clear();
        _all.AddRange(result);
        TotalCount = _all.Count;
        ApplyFilter();
    }

    internal void SetError(string message)
    {
        _all.Clear();
        Packages.Clear();
        TotalCount = 0;
        VisibleCount = 0;
        ErrorMessage = message;
    }

    private void ApplyFilter()
    {
        Packages.Clear();
        var q = SearchText.Trim();
        IEnumerable<ContainerPackage> view = _all;
        if (q.Length > 0)
            view = view.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));

        var sorted = view
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var p in sorted)
            Packages.Add(p);
        VisibleCount = sorted.Count;
    }
}
