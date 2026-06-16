using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Allpaca.Chrome;
using Allpaca.Models;
using Allpaca.ViewModels;
using Avalonia.Interactivity;
using NLog;

namespace Allpaca.Views;

public partial class ContainerInspectorWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly ContainerInspectorViewModel _vm = new();
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<ContainerPackage>>> _probe;
    private CancellationTokenSource _cts = new();

    public ContainerInspectorWindow(
        Func<string, CancellationToken, Task<IReadOnlyList<ContainerPackage>>> probe,
        string containerName)
    {
        InitializeComponent();
        _probe = probe;
        DataContext = _vm;
        _vm.ContainerName = containerName;
        _vm.Title = $"Pakete in {containerName}";
        Title = _vm.Title;
    }

    // Parameterloser Konstruktor nur fuer den Avalonia-XAML-Loader (Preview).
    // Liefert eine leere Probe-Funktion, damit der Designer keine Side-Effects ausloest.
    public ContainerInspectorWindow()
        : this((_, _) => Task.FromResult<IReadOnlyList<ContainerPackage>>(Array.Empty<ContainerPackage>()), "")
    {
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = LoadAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        _cts.Dispose();
    }

    private async Task LoadAsync()
    {
        _vm.IsLoading = true;
        try
        {
            var list = await _probe(_vm.ContainerName, _cts.Token);
            if (list.Count == 0)
                _vm.SetError("Keine Pakete gefunden – ist im Container ein unterstützter Package-Manager installiert (dpkg/rpm/pacman/apk)?");
            else
                _vm.SetResult(list);

            Log.Info("Container '{0}': {1} Pakete", _vm.ContainerName, list.Count);
        }
        catch (OperationCanceledException)
        {
            // Window wurde zugemacht waehrend des Ladens - kein UI-Update mehr noetig.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Container-Probe fehlgeschlagen: {0}", _vm.ContainerName);
            _vm.SetError($"Fehler beim Auflisten: {ex.Message}");
        }
        finally
        {
            _vm.IsLoading = false;
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        // Frisches CTS, falls altes schon disposed/cancelled war.
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _ = LoadAsync();
    }
}
