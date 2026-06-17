using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Allpaca.Chrome;
using Allpaca.Services.Ai;
using Allpaca.ViewModels;
using NLog;

namespace Allpaca.Views;

public partial class CleanupAnalysisWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly CleanupAnalysisViewModel _vm = new();
    private CancellationTokenSource _cts = new();

    /// <summary>Wird vom Aufrufer (MainWindow) gesetzt: System+User-Prompt -> KI-Antwort.</summary>
    public Func<string, string, CancellationToken, Task<string>>? AnalyzeHandler { get; set; }

    public CleanupAnalysisWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.CloseRequested += Close;
        _vm.AnalyzeRequested += () => _ = RunAsync(_lastPackages ?? Array.Empty<PackageItemViewModel>());
    }

    private IReadOnlyList<PackageItemViewModel>? _lastPackages;

    public async Task RunAsync(IReadOnlyList<PackageItemViewModel> packages)
    {
        _lastPackages = packages;
        _vm.PackageCount = packages.Count(p => !p.Model.IsRuntime);
        _vm.AnalysisText = "";
        _vm.ErrorText = null;
        _vm.State = OperationState.Running;

        if (AnalyzeHandler is null)
        {
            _vm.ErrorText = "KI-Handler nicht eingehängt – Allpaca-Bug.";
            _vm.State = OperationState.Failed;
            return;
        }

        // Frisches CTS bei jedem Run, damit "Nochmal analysieren" sauber neu startet.
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var userPrompt = CleanupPromptBuilder.BuildUserPrompt(packages);
            var answer = await AnalyzeHandler(
                CleanupPromptBuilder.SystemPrompt, userPrompt, _cts.Token);

            _vm.AnalysisText = (answer ?? "").Trim();
            if (string.IsNullOrWhiteSpace(_vm.AnalysisText))
            {
                _vm.ErrorText = "Leere Antwort vom Provider erhalten.";
                _vm.State = OperationState.Failed;
            }
            else
            {
                _vm.State = OperationState.Succeeded;
                Log.Info("Aufraeum-Analyse fertig ({0} Pakete)", _vm.PackageCount);
            }
        }
        catch (OperationCanceledException)
        {
            _vm.State = OperationState.Cancelled;
        }
        catch (Exception ex)
        {
            _vm.ErrorText = ex.Message;
            _vm.State = OperationState.Failed;
            Log.Warn(ex, "Aufraeum-Analyse fehlgeschlagen");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        _cts.Dispose();
    }
}
