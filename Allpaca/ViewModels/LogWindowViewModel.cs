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
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private OperationState _state = OperationState.Running;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int? _exitCode;

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
        OperationState.Succeeded => "#2BB673", // gruen
        OperationState.Failed => "#E25555",    // rot
        OperationState.Cancelled => "#9AA0A8", // grau
        _ => "#9AA0A8",
    }));

    /// <summary>Wird vom Code-Behind beim Klick auf "Abbrechen" gerufen, um die
    /// laufende Stream-Operation per CancellationToken zu beenden.</summary>
    public event Action? CancelRequested;

    /// <summary>Wird vom Code-Behind gesetzt, sobald die Operation komplett durch ist
    /// (egal ob success/fail/cancel) - das Fenster soll dann schliessbar sein.</summary>
    public event Action? CloseRequested;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => CancelRequested?.Invoke();
    private bool CanCancel() => State == OperationState.Running;

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => CloseRequested?.Invoke();
    private bool CanClose() => State != OperationState.Running;
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
