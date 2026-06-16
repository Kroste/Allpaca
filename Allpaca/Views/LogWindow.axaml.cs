using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Allpaca.Chrome;
using Allpaca.Models;
using Allpaca.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NLog;

namespace Allpaca.Views;

public partial class LogWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly LogWindowViewModel _vm = new();
    private CancellationTokenSource _cts = new();

    public LogWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.CancelRequested += () =>
        {
            if (!_cts.IsCancellationRequested) _cts.Cancel();
        };
        _vm.CloseRequested += Close;
    }

    /// <summary>
    /// Startet die uebergebene Stream-Operation, fuettert die Live-Log-Liste mit den
    /// Zeilen und kippt am Ende den OperationState passend zu Erfolg/Fehler/Cancel.
    /// Laeuft komplett auf dem UI-Thread (kein ConfigureAwait(false) - die Continuations
    /// muessen die ObservableCollection auf dem UI-Thread mutieren).
    /// </summary>
    public async Task RunAsync(OperationContext context, Func<CancellationToken, IAsyncEnumerable<ProgressLine>> work)
    {
        _vm.Title = context.Title;
        Title = context.Title;
        _vm.State = OperationState.Running;
        _vm.ExitCode = null;
        _vm.RequiresReboot = context.RequiresReboot;
        _vm.Lines.Clear();

        var scroll = this.FindControl<ScrollViewer>("LogScroll");

        try
        {
            await foreach (var line in work(_cts.Token).WithCancellation(_cts.Token))
            {
                if (line.ExitCode is int code)
                {
                    // Marker-Zeile vom ProcessRunner: nicht anzeigen, nur State + ExitCode merken.
                    _vm.ExitCode = code;
                    continue;
                }
                _vm.Lines.Add(line);
                scroll?.ScrollToEnd();
            }
            _vm.State = _vm.ExitCode is null or 0
                ? OperationState.Succeeded
                : OperationState.Failed;
            Log.Info("Operation fertig: {0} (Exit={1}, {2} Zeilen)",
                context.Title, _vm.ExitCode?.ToString() ?? "?", _vm.Lines.Count);
        }
        catch (OperationCanceledException)
        {
            _vm.State = OperationState.Cancelled;
            Log.Info("Operation abgebrochen: {0}", context.Title);
        }
        catch (Exception ex)
        {
            _vm.Lines.Add(new ProgressLine($"[Exception] {ex.Message}", true));
            _vm.State = OperationState.Failed;
            Log.Error(ex, "Operation fehlgeschlagen: {0}", context.Title);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        _cts.Dispose();
    }
}
