using System;
using System.Collections.Generic;
using System.Linq;
using Allpaca.Chrome;
using Allpaca.Services;
using Allpaca.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Allpaca.Views;

public partial class MainWindow : ChromeWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Hauptfenster soll NICHT auf Esc zugehen - sonst killt der User
    /// versehentlich die ganze App. Subfenster bleiben beim ChromeWindow-Default.</summary>
    protected override bool CloseOnEscape => false;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Reihenfolge: Tastenkuerzel zuerst, dann an die Basis fuer Esc-Logik etc.
        if (!e.Handled && DataContext is MainWindowViewModel vm)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                switch (e.Key)
                {
                    case Key.R:
                        if (vm.RefreshCommand.CanExecute(null))
                            vm.RefreshCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.I:
                        if (vm.OpenInstallCommand.CanExecute(null))
                            vm.OpenInstallCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.F:
                        if (this.FindControl<TextBox>("SearchBox") is { } sb)
                        {
                            sb.Focus();
                            sb.SelectAll();
                        }
                        e.Handled = true;
                        break;
                    case Key.OemComma:
                        if (vm.OpenAiSettingsCommand.CanExecute(null))
                            vm.OpenAiSettingsCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.F5)
            {
                if (vm.RefreshCommand.CanExecute(null))
                    vm.RefreshCommand.Execute(null);
                e.Handled = true;
            }
        }

        base.OnKeyDown(e);
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
        win.DiagnoseHandler = StreamAiAsync;
        win.Show(this);
        await win.RunAsync(ctx, work);
    }

    /// <summary>Bridge ins KI-Subsystem - LogWindow/CleanupAnalysis/SearchWindow rufen
    /// das, sobald sie die KI brauchen. Liest CurrentAi vom MainWindow-VM, baut den
    /// Assistant ueber die Factory und streamt die Antwort. Caller akkumulieren bei
    /// Bedarf - LogWindow/Cleanup zeigen live an, Search bufferd und parst am Ende.</summary>
    private System.Collections.Generic.IAsyncEnumerable<string> StreamAiAsync(
        string systemPrompt, string userPrompt, System.Threading.CancellationToken ct)
    {
        if (DataContext is not MainWindowViewModel vm)
            throw new InvalidOperationException("MainWindow ohne ViewModel - sollte nicht passieren.");

        var assistant = Allpaca.Services.Ai.AiAssistantFactory.Create(vm.CurrentAi);
        if (!assistant.IsConfigured)
            throw new InvalidOperationException(
                "KI ist noch nicht konfiguriert. Öffne die Einstellungen (⚙) und wähle Provider + Modell.");

        // Hartes Timeout - bei sehr grossen Ollama-Modellen koennen einzelne Antworten
        // zaeh werden. Wir wrappen die Stream-Iteration mit einem linked CTS, damit
        // sowohl externe Cancellation als auch das 120-s-Timeout sauber durchschlagen.
        return StreamWithTimeoutAsync(assistant, systemPrompt, userPrompt, ct);
    }

    private static async System.Collections.Generic.IAsyncEnumerable<string> StreamWithTimeoutAsync(
        Allpaca.Services.Ai.IAiAssistant assistant,
        string systemPrompt, string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)
    {
        using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(120));
        await foreach (var chunk in assistant.CompleteStreamAsync(systemPrompt, userPrompt, linked.Token))
            yield return chunk;
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

    private System.Threading.Tasks.Task<AppPreferences?> OpenSettingsWindowAsync(
        AppPreferences current)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<AppPreferences?>();
        var settingsVm = new SettingsWindowViewModel(current);

        AppPreferences? result = null;
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
            AnalyzeHandler = StreamAiAsync,  // gleicher KI-Bridge wie LogWindow
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
            AiCompletion = StreamAiAsync,
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
