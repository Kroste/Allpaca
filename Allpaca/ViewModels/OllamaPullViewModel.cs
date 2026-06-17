using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Allpaca.ViewModels;

public partial class OllamaPullViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "Modell wird geladen";

    [ObservableProperty] private string _modelName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string _phase = "Starte …";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    [NotifyPropertyChangedFor(nameof(ProgressValue))]
    private long? _completedBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    [NotifyPropertyChangedFor(nameof(ProgressValue))]
    private long? _totalBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private OperationState _state = OperationState.Running;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _errorMessage;

    public bool HasProgress => TotalBytes is > 0 && CompletedBytes is not null;

    /// <summary>0..100. Wenn HasProgress=false, bleibt 0 - der ProgressBar laeuft dann
    /// indeterminate.</summary>
    public double ProgressValue =>
        HasProgress ? (double)CompletedBytes!.Value / TotalBytes!.Value * 100.0 : 0;

    public string StatusText
    {
        get
        {
            if (State == OperationState.Cancelled) return "Abgebrochen.";
            if (State == OperationState.Failed) return ErrorMessage ?? "Fehler.";
            if (State == OperationState.Succeeded) return "Fertig!";
            if (HasProgress)
            {
                var mbDone = CompletedBytes!.Value / 1024.0 / 1024.0;
                var mbTotal = TotalBytes!.Value / 1024.0 / 1024.0;
                return $"{Phase} – {mbDone:F0} / {mbTotal:F0} MB";
            }
            return Phase;
        }
    }

    public event Action? CancelRequested;
    public event Action? CloseRequested;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => CancelRequested?.Invoke();
    private bool CanCancel() => State == OperationState.Running;

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => CloseRequested?.Invoke();
    private bool CanClose() => State != OperationState.Running;
}
