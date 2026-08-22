using System.Collections.ObjectModel;
using Allpaca.Models;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Allpaca.ViewModels;

public partial class LogWindowViewModel : ObservableObject
{
    public ObservableCollection<ProgressLine> Lines { get; } = new();

    [ObservableProperty] private string _title = "Operation";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    [NotifyPropertyChangedFor(nameof(ShowRebootHint))]
    [NotifyPropertyChangedFor(nameof(ShowUntrustedTapHint))]
    [NotifyPropertyChangedFor(nameof(ShowAiDiagnoseSection))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    [NotifyCanExecuteChangedFor(nameof(TrustTapCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiagnoseAiCommand))]
    private OperationState _state = OperationState.Running;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int? _exitCode;

    /// <summary>Wird vom Code-Behind aus dem OperationContext gespeist - der ViewModel
    /// reicht den Wert nur an die View durch (ShowRebootHint).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRebootHint))]
    private bool _requiresReboot;

    /// <summary>Reboot-Hinweis ist nur dann sichtbar, wenn die Operation erfolgreich war
    /// UND der Context "RequiresReboot" signalisiert hatte.</summary>
    public bool ShowRebootHint => RequiresReboot && State == OperationState.Succeeded;

    /// <summary>Wird vom Code-Behind gesetzt, sobald in einer Log-Zeile das
    /// "untrusted tap"-Muster erkannt wurde. Triggert ShowUntrustedTapHint.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUntrustedTapHint))]
    [NotifyCanExecuteChangedFor(nameof(TrustTapCommand))]
    private string? _untrustedTapName;

    /// <summary>Hinweis-Banner erscheint, wenn ein untrusted-tap-Fehler vorliegt UND
    /// die Operation gescheitert ist (nicht während des Laufs).</summary>
    public bool ShowUntrustedTapHint =>
        !string.IsNullOrEmpty(UntrustedTapName) && State == OperationState.Failed;

    // --- KI-Fehlerdiagnose ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiDiagnosis))]
    [NotifyPropertyChangedFor(nameof(AiDiagnoseButtonLabel))]
    [NotifyCanExecuteChangedFor(nameof(DiagnoseAiCommand))]
    private string? _aiDiagnosis;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DiagnoseAiCommand))]
    [NotifyPropertyChangedFor(nameof(AiDiagnoseButtonLabel))]
    private bool _isAiDiagnosing;

    [ObservableProperty] private string? _aiDiagnosisError;

    public bool HasAiDiagnosis => !string.IsNullOrEmpty(AiDiagnosis);

    /// <summary>Diagnose-Sektion ist nur sichtbar, wenn die Operation gescheitert ist.
    /// Cancelled/Succeeded blenden sie aus, damit das Fenster ruhig bleibt.</summary>
    public bool ShowAiDiagnoseSection => State == OperationState.Failed;

    public string AiDiagnoseButtonLabel => IsAiDiagnosing
        ? "Analysiere …"
        : HasAiDiagnosis
            ? "Nochmal analysieren"
            : "Analysieren";

    /// <summary>Wird vom Code-Behind gesetzt: ruft den KI-Provider mit Title + ExitCode +
    /// Log-Tail und liefert die Diagnose-Antwort.</summary>
    public event Action? DiagnoseRequested;

    public string StatusText => State switch
    {
        OperationState.Running => "läuft …",
        OperationState.Succeeded => "Fertig",
        OperationState.Failed => ExitCode is int c ? $"Fehler (Exit {c})" : "Fehler",
        OperationState.Cancelled => "Abgebrochen",
        _ => "",
    };

    public IBrush StatusBrush => new SolidColorBrush(Color.Parse(State switch
    {
        OperationState.Running => "#F5A623",   // gelb
        OperationState.Succeeded => "#2BB673", // grün
        OperationState.Failed => "#E25555",    // rot
        OperationState.Cancelled => "#9AA0A8", // grau
        _ => "#9AA0A8",
    }));

    /// <summary>Wird vom Code-Behind beim Klick auf "Abbrechen" gerufen, um die
    /// laufende Stream-Operation per CancellationToken zu beenden.</summary>
    public event Action? CancelRequested;

    /// <summary>Wird vom Code-Behind gesetzt, sobald die Operation komplett durch ist
    /// (egal ob success/fail/cancel) - das Fenster soll dann schließbar sein.</summary>
    public event Action? CloseRequested;

    /// <summary>Triggert vom Code-Behind aus den "brew trust &lt;tap&gt;"-Lauf im selben
    /// Fenster.</summary>
    public event Action<string>? TrustTapRequested;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => CancelRequested?.Invoke();
    private bool CanCancel() => State == OperationState.Running;

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => CloseRequested?.Invoke();
    private bool CanClose() => State != OperationState.Running;

    [RelayCommand(CanExecute = nameof(CanTrustTap))]
    private void TrustTap()
    {
        if (UntrustedTapName is { Length: > 0 } tap)
            TrustTapRequested?.Invoke(tap);
    }
    private bool CanTrustTap() => ShowUntrustedTapHint;

    [RelayCommand(CanExecute = nameof(CanDiagnoseAi))]
    private void DiagnoseAi() => DiagnoseRequested?.Invoke();

    private bool CanDiagnoseAi() =>
        State == OperationState.Failed && !IsAiDiagnosing;
}

public enum OperationState
{
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

public static class ProgressLineConverters
{
    // Fehlerzeilen rot, normale Zeilen hellgrau - via Avalonias eingebautem FuncValueConverter.
    public static readonly IValueConverter ErrorOrNormal =
        new FuncValueConverter<bool, IBrush>(isError =>
            isError
                ? new SolidColorBrush(Color.Parse("#E25555"))
                : new SolidColorBrush(Color.Parse("#C7CCD2")));
}
