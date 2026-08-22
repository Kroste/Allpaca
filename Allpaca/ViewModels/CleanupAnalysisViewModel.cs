using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Allpaca.ViewModels;

public partial class CleanupAnalysisViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "KI-Aufräum-Analyse";

    [ObservableProperty] private int _packageCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusBrushColor))]
    [NotifyPropertyChangedFor(nameof(IsAnalyzing))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private OperationState _state = OperationState.Running;

    public bool IsAnalyzing => State == OperationState.Running;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string _analysisText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _errorText;

    public string StatusText => State switch
    {
        OperationState.Running => $"Analysiere {PackageCount} Pakete …",
        OperationState.Succeeded => $"Analyse fertig ({PackageCount} Pakete)",
        OperationState.Failed => ErrorText ?? "Fehler.",
        OperationState.Cancelled => "Abgebrochen.",
        _ => "",
    };

    public string StatusBrushColor => State switch
    {
        OperationState.Running => "#F5A623",
        OperationState.Succeeded => "#2BB673",
        OperationState.Failed => "#E25555",
        OperationState.Cancelled => "#9AA0A8",
        _ => "#9AA0A8",
    };

    public event Action? CloseRequested;

    /// <summary>Triggert die Re-Analyse - der Code-Behind fängt das.</summary>
    public event Action? AnalyzeRequested;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private void Analyze() => AnalyzeRequested?.Invoke();
    private bool CanAnalyze() => State != OperationState.Running;

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => CloseRequested?.Invoke();
    private bool CanClose() => State != OperationState.Running;
}
