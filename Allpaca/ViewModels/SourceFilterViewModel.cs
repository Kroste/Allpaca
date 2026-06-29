using Allpaca.Models;
using Allpaca.Services;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Allpaca.ViewModels;

public partial class SourceFilterViewModel : ObservableObject
{
    public PackageSourceKind Kind { get; }
    public string Label { get; }
    public IBrush Brush { get; }

    /// <summary>Konkreter Installations-Tipp fuer diese Quelle - landet im ToolTip
    /// beim "nicht verfuegbar"-Text.</summary>
    public string? RecoveryHint { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isAvailable = true;
    [ObservableProperty] private string? _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdates))]
    private int _updateCount;

    /// <summary>True, sobald in dieser Quelle mindestens ein Eintrag ein verfuegbares
    /// Update hat - treibt das gruene "↑ N"-Badge im Sidebar-Item.</summary>
    public bool HasUpdates => UpdateCount > 0;

    public SourceFilterViewModel(PackageSourceKind kind, string label, string color)
    {
        Kind = kind;
        Label = label;
        Brush = new SolidColorBrush(Color.Parse(color));
        RecoveryHint = SourceRecoveryHints.For(kind);
    }
}
