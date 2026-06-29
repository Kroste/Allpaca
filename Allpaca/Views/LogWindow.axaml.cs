using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Allpaca.Chrome;
using Allpaca.Models;
using Allpaca.Services.Ai;
using Allpaca.Services.Sources;
using Allpaca.ViewModels;
using Avalonia.Controls;
using NLog;

namespace Allpaca.Views;

public partial class LogWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly LogWindowViewModel _vm = new();
    private CancellationTokenSource _cts = new();

    /// <summary>Wird vom Aufrufer (MainWindow.RunOperationAsync) gesetzt, damit das
    /// LogWindow auf "Tap vertrauen"-Klicks reagieren kann.</summary>
    public Func<string, CancellationToken, IAsyncEnumerable<ProgressLine>>? TrustTapHandler { get; set; }

    /// <summary>Wird vom Aufrufer gesetzt: bekommt System+User-Prompt und streamt die
    /// KI-Antwort zurueck. Wird beim Klick auf "Analysieren" im Failed-Banner aufgerufen;
    /// jedes yielded Chunk wird live an AiDiagnosis angehaengt.</summary>
    public Func<string, string, CancellationToken, IAsyncEnumerable<string>>? DiagnoseHandler { get; set; }

    public LogWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.CancelRequested += () =>
        {
            if (!_cts.IsCancellationRequested) _cts.Cancel();
        };
        _vm.CloseRequested += Close;
        _vm.TrustTapRequested += tap => _ = RunTrustAsync(tap);
        _vm.DiagnoseRequested += () => _ = HandleDiagnoseAsync();
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
        _vm.RequiresReboot = context.RequiresReboot;
        _vm.Lines.Clear();
        _vm.UntrustedTapName = null;
        _vm.AiDiagnosis = null;
        _vm.AiDiagnosisError = null;

        await RunStreamAsync(work, context.Title);
    }

    private async Task HandleDiagnoseAsync()
    {
        _vm.AiDiagnosisError = null;

        if (DiagnoseHandler is null)
        {
            _vm.AiDiagnosisError = "KI nicht eingehängt – Allpaca-Bug, bitte melden.";
            return;
        }

        _vm.IsAiDiagnosing = true;
        _vm.AiDiagnosis = "";
        try
        {
            var userPrompt = DiagnosisPromptBuilder.BuildUserPrompt(
                _vm.Title, _vm.ExitCode, _vm.Lines.ToList());

            // Chunks streamen live ins AiDiagnosis-Property - StringBuilder als
            // Akkumulator, weil String-Concat in der Schleife O(n^2) waere.
            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in DiagnoseHandler(
                DiagnosisPromptBuilder.SystemPrompt, userPrompt, _cts.Token))
            {
                sb.Append(chunk);
                _vm.AiDiagnosis = sb.ToString();
            }

            var final = sb.ToString().Trim();
            _vm.AiDiagnosis = final;
            if (string.IsNullOrWhiteSpace(final))
                _vm.AiDiagnosisError = "Leere Antwort vom Provider erhalten.";
        }
        catch (OperationCanceledException)
        {
            _vm.AiDiagnosisError = "Analyse abgebrochen.";
        }
        catch (Exception ex)
        {
            _vm.AiDiagnosisError = $"Fehler: {ex.Message}";
            Log.Warn(ex, "AI-Diagnose fehlgeschlagen");
        }
        finally { _vm.IsAiDiagnosing = false; }
    }

    /// <summary>
    /// Sekundaerlauf im selben Fenster - "brew trust &lt;tap&gt;" als Folgeoperation
    /// nach einem untrusted-tap-Fehler. Behaelt die bisherigen Log-Zeilen + setzt
    /// einen Separator, damit der User sehen kann was er angestossen hat.
    /// </summary>
    private async Task RunTrustAsync(string tap)
    {
        if (TrustTapHandler is null) return;

        _cts.Dispose();
        _cts = new CancellationTokenSource();

        _vm.UntrustedTapName = null;
        _vm.Lines.Add(new ProgressLine($"--- brew trust {tap} ---", false));

        await RunStreamAsync(ct => TrustTapHandler(tap, ct), $"brew trust {tap}");
    }

    private async Task RunStreamAsync(
        Func<CancellationToken, IAsyncEnumerable<ProgressLine>> work,
        string logTitle)
    {
        _vm.State = OperationState.Running;
        _vm.ExitCode = null;

        var scroll = this.FindControl<ScrollViewer>("LogScroll");

        try
        {
            await foreach (var line in work(_cts.Token).WithCancellation(_cts.Token))
            {
                if (line.ExitCode is int code)
                {
                    // Marker-Zeile vom ProcessRunner: nicht anzeigen, nur ExitCode merken.
                    _vm.ExitCode = code;
                    continue;
                }
                _vm.Lines.Add(line);

                // Homebrew-untrusted-tap-Muster scannen - sobald ein Treffer da ist,
                // erscheint nach Fail der Hinweis-Banner mit "Tap vertrauen".
                if (line.IsError && _vm.UntrustedTapName is null)
                {
                    var tap = UntrustedTapDetector.Extract(line.Text);
                    if (tap is not null) _vm.UntrustedTapName = tap;
                }

                scroll?.ScrollToEnd();
            }
            _vm.State = _vm.ExitCode is null or 0
                ? OperationState.Succeeded
                : OperationState.Failed;
            Log.Info("Operation fertig: {0} (Exit={1}, {2} Zeilen)",
                logTitle, _vm.ExitCode?.ToString() ?? "?", _vm.Lines.Count);
        }
        catch (OperationCanceledException)
        {
            _vm.State = OperationState.Cancelled;
            Log.Info("Operation abgebrochen: {0}", logTitle);
        }
        catch (Exception ex)
        {
            _vm.Lines.Add(new ProgressLine($"[Exception] {ex.Message}", true));
            _vm.State = OperationState.Failed;
            Log.Error(ex, "Operation fehlgeschlagen: {0}", logTitle);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        _cts.Dispose();
    }
}
