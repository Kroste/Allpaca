using System;
using System.Threading;
using System.Threading.Tasks;
using Allpaca.Chrome;
using Allpaca.Services.Ai;
using Allpaca.ViewModels;
using NLog;

namespace Allpaca.Views;

public partial class OllamaPullWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly OllamaPullViewModel _vm = new();
    private CancellationTokenSource _cts = new();

    public OllamaPullWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.CancelRequested += () =>
        {
            if (!_cts.IsCancellationRequested) _cts.Cancel();
        };
        _vm.CloseRequested += Close;
    }

    public async Task PullAsync(OllamaModelService service, string endpoint, string model)
    {
        _vm.ModelName = model;
        _vm.Title = $"Lade „{model}“";
        Title = _vm.Title;
        _vm.State = OperationState.Running;
        _vm.ErrorMessage = null;
        _vm.Phase = "Starte …";
        _vm.CompletedBytes = null;
        _vm.TotalBytes = null;

        try
        {
            await foreach (var evt in service.PullAsync(endpoint, model, _cts.Token))
            {
                if (evt.IsError)
                {
                    _vm.ErrorMessage = evt.ErrorMessage;
                    _vm.State = OperationState.Failed;
                    Log.Warn("Pull {0}: {1}", model, evt.ErrorMessage);
                    return;
                }

                _vm.Phase = evt.Status;
                _vm.CompletedBytes = evt.Completed;
                _vm.TotalBytes = evt.Total;

                if (string.Equals(evt.Status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    _vm.State = OperationState.Succeeded;
                    Log.Info("Pull {0}: success", model);
                    return;
                }
            }

            // Stream zu Ende ohne explizites "success" - als Erfolg werten (alte Ollama-
            // Versionen schliessen den Stream ohne success-Event).
            _vm.State = OperationState.Succeeded;
            Log.Info("Pull {0}: Stream beendet (kein expliziter success-Event)", model);
        }
        catch (OperationCanceledException)
        {
            _vm.State = OperationState.Cancelled;
            Log.Info("Pull {0}: abgebrochen", model);
        }
        catch (Exception ex)
        {
            _vm.ErrorMessage = ex.Message;
            _vm.State = OperationState.Failed;
            Log.Error(ex, "Pull {0}", model);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        _cts.Dispose();
    }
}
