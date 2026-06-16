using Allpaca.Models;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Allpaca.ViewModels;

public partial class SourceFilterViewModel : ObservableObject
{
    public PackageSourceKind Kind { get; }
    public string Label { get; }
    public IBrush Brush { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isAvailable = true;
    [ObservableProperty] private string? _status;

    public SourceFilterViewModel(PackageSourceKind kind, string label, string color)
    {
        Kind = kind;
        Label = label;
        Brush = new SolidColorBrush(Color.Parse(color));
    }
}
