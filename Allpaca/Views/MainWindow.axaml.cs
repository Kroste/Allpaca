using System;
using System.Collections.Generic;
using System.Linq;
using Allpaca.Chrome;
using Allpaca.ViewModels;
using Avalonia.Controls;

namespace Allpaca.Views;

public partial class MainWindow : ChromeWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel vm)
        {
            // ViewModel kennt keine View-Typen - "oeffne ein LogWindow" und "frag den
            // User per modalem Dialog" werden hier injiziert. DataContext steht erst in
            // OnOpened sicher (siehe App.OnFrameworkInitializationCompleted).
            vm.RunOperation ??= RunOperationAsync;
            vm.ConfirmAsync ??= req => ConfirmWindow.AskAsync(this, req);
            vm.OpenContainerInspector ??= name =>
            {
                var win = new ContainerInspectorWindow(vm.ProbeContainerPackagesAsync, name);
                win.Show(this);
            };
            vm.OpenInstallSearch ??= () => OpenSearchWindow(vm);
            vm.OpenSettings ??= current => OpenSettingsWindowAsync(current);
            vm.OpenCleanupAnalysis ??= packages => OpenCleanupAnalysisWindow(packages);

            if (vm.RefreshCommand.CanExecute(null))
                vm.RefreshCommand.Execute(null);
        }
    }

    private async System.Threading.Tasks.Task RunOperationAsync(
        OperationContext ctx,
        Func<System.Threading.CancellationToken,
             System.Collections.Generic.IAsyncEnumerable<Allpaca.Models.ProgressLine>> work)
    {
        var win = new LogWindow();
        win.TrustTapHandler = TrustHomebrewTapAsync;
        win.DiagnoseHandler = DiagnoseWithAiAsync;
        win.Show(this);
        await win.RunAsync(ctx, work);
    }

    /// <summary>Bridge ins KI-Subsystem - LogWindow ruft das auf, wenn der User auf
    /// "Analysieren" im Fehler-Banner klickt. Liest CurrentAi vom MainWindow-VM,
    /// baut den Assistant ueber die Factory und schickt das Prompt-Paar durch.</summary>
    private async System.Threading.Tasks.Task<string> DiagnoseWithAiAsync(
        string systemPrompt, string userPrompt, System.Threading.CancellationToken ct)
    {
        if (DataContext is not MainWindowViewModel vm)
            throw new InvalidOperationException("MainWindow ohne ViewModel - sollte nicht passieren.");

        var assistant = Allpaca.Services.Ai.AiAssistantFactory.Create(vm.CurrentAi);
        if (!assistant.IsConfigured)
            throw new InvalidOperationException(
                "KI ist noch nicht konfiguriert. Öffne die Einstellungen (⚙) und wähle Provider + Modell.");

        // Hartes Timeout fuer die Diagnose - Ollama mit grossen Modellen kann zaeh sein.
        using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(120));
        return await assistant.CompleteAsync(systemPrompt, userPrompt, linked.Token);
    }

    /// <summary>Bridge ins HomebrewSource, damit das LogWindow auf "Tap vertrauen"-Klicks
    /// reagieren kann, ohne selber Sources zu kennen.</summary>
    private System.Collections.Generic.IAsyncEnumerable<Allpaca.Models.ProgressLine> TrustHomebrewTapAsync(
        string tap, System.Threading.CancellationToken ct)
    {
        if (DataContext is MainWindowViewModel vm
            && vm.SourcesByKind.TryGetValue(Allpaca.Models.PackageSourceKind.Homebrew, out var src)
            && src is Allpaca.Services.Sources.HomebrewSource brew)
        {
            return brew.TrustTapAsync(tap, ct);
        }
        return EmptyProgressStream();
    }

    private static async System.Collections.Generic.IAsyncEnumerable<Allpaca.Models.ProgressLine> EmptyProgressStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct = default)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        yield return new Allpaca.Models.ProgressLine("Homebrew nicht verfügbar.", true);
    }

    private System.Threading.Tasks.Task<Allpaca.Services.Ai.AiSettings?> OpenSettingsWindowAsync(
        Allpaca.Services.Ai.AiSettings current)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<Allpaca.Services.Ai.AiSettings?>();
        var settingsVm = new SettingsWindowViewModel(current);

        Allpaca.Services.Ai.AiSettings? result = null;
        settingsVm.Saved += s => result = s;

        var win = new SettingsWindow(settingsVm);
        win.Closed += (_, _) => tcs.TrySetResult(result);
        win.Show(this);
        return tcs.Task;
    }

    private void OpenCleanupAnalysisWindow(System.Collections.Generic.IReadOnlyList<PackageItemViewModel> packages)
    {
        var win = new CleanupAnalysisWindow
        {
            AnalyzeHandler = DiagnoseWithAiAsync,  // gleicher KI-Bridge wie LogWindow
        };
        win.Show(this);
        _ = win.RunAsync(packages);
    }

    private void OpenSearchWindow(MainWindowViewModel vm)
    {
        var searchVm = new SearchWindowViewModel(vm.SourcesByKind)
        {
            RunOperation = RunOperationAsync,
            ConfirmAsync = req => ConfirmWindow.AskAsync(this, req),
            AiCompletion = DiagnoseWithAiAsync,
            AfterInstall = () =>
            {
                // Liste nach Install refreshen, damit der neue Eintrag sichtbar wird.
                if (vm.RefreshCommand.CanExecute(null))
                    vm.RefreshCommand.Execute(null);
            },
        };
        new SearchWindow(searchVm).Show(this);
    }

    private void OnInfoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => new InfoWindow().ShowDialog(this);

    private void OnPackagesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // ListBox.SelectedItems ist IList und nicht direkt bindbar - wir spiegeln
        // den aktuellen Zustand auf das ViewModel, damit Batch-Buttons + Banner
        // rechtzeitig aktualisieren.
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is not ListBox lb) return;

        var items = lb.SelectedItems is { } sel
            ? sel.Cast<PackageItemViewModel>().ToList()
            : new List<PackageItemViewModel>();
        vm.SelectedItems = items;
    }
}
