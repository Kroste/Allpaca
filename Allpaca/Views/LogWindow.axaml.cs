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
    public async Task RunAsync(string title, Func<CancellationToken, IAsyncEnumerable<ProgressLine>> work)
    {
        _vm.Title = title;
        Title = title;
        _vm.State = OperationState.Running;
        _vm.Lines.Clear();

        var scroll = this.FindControl<ScrollViewer>("LogScroll");

        try
        {
            await foreach (var line in work(_cts.Token).WithCancellation(_cts.Token))
            {
                _vm.Lines.Add(line);
                scroll?.ScrollToEnd();
            }
            _vm.State = OperationState.Succeeded;
            Log.Info("Operation fertig: {0} ({1} Zeilen)", title, _vm.Lines.Count);
        }
        catch (OperationCanceledException)
        {
            _vm.State = OperationState.Cancelled;
            Log.Info("Operation abgebrochen: {0}", title);
        }
        catch (Exception ex)
        {
            _vm.Lines.Add(new ProgressLine($"[Exception] {ex.Message}", true));
            _vm.State = OperationState.Failed;
            Log.Error(ex, "Operation fehlgeschlagen: {0}", title);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        _cts.Dispose();
    }
}
