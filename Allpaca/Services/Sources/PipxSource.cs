using System.Runtime.CompilerServices;
using Allpaca.Models;
using NLog;

namespace Allpaca.Services.Sources;

/// <summary>
/// Verwaltet Python-CLI-Tools, die über pipx in eigene venvs installiert sind
/// (z. B. httpie, glances, yt-dlp, gh). Auf Bazzite häufig genutzt; rootless,
/// kein pkexec nötig. Search ist absichtlich nicht implementiert - pipx selbst
/// hat keine "search"-Sub, das läuft normalerweise via "pip search" gegen PyPI
/// und ist seit Jahren deaktiviert.
/// </summary>
public sealed class PipxSource : IPackageSource
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    // pipx ist in GUI-Sessions oft auch nicht im PATH - typisch via pip --user installiert
    // landet's unter ~/.local/bin. Wir probieren beide Pfade.
    private static readonly string[] Candidates =
    {
        "pipx",
        "/usr/bin/pipx",
        "/usr/local/bin/pipx",
    };

    private readonly ProcessRunner _runner;
    private string? _pipx;

    public PipxSource(ProcessRunner runner) => _runner = runner;

    public PackageSourceKind Kind => PackageSourceKind.Pipx;
    public string DisplayName => "pipx";
    public PackageCapabilities Capabilities => new()
    {
        CanSearch = false, CanInstall = true, CanUninstall = true, CanUpdate = true,
        RequiresRoot = false, RequiresReboot = false,
    };

    private async Task<string?> ResolveAsync(CancellationToken ct)
    {
        if (_pipx is not null) return _pipx;

        // Erst PATH (oder ~/.local/bin sofern im PATH), dann Fallback-Pfade.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new List<string>(Candidates) { Path.Combine(home, ".local/bin/pipx") };

        foreach (var c in candidates)
        {
            if ((await _runner.RunAsync(c, new[] { "--version" }, ct)).Success)
            {
                _pipx = c;
                Log.Info("pipx gefunden: {0}", c);
                return _pipx;
            }
        }
        return null;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => await ResolveAsync(ct) is not null;

    public async Task<IReadOnlyList<PackageInfo>> ListInstalledAsync(CancellationToken ct = default)
    {
        var pipx = await ResolveAsync(ct);
        if (pipx is null) return Array.Empty<PackageInfo>();

        var r = await _runner.RunAsync(pipx, new[] { "list", "--json" }, ct);
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut))
        {
            Log.Warn("pipx list fehlgeschlagen: {0}", r.StdErr);
            return Array.Empty<PackageInfo>();
        }

        return PipxListParser.Parse(r.StdOut);
    }

    public Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PackageInfo>>(Array.Empty<PackageInfo>());

    public async IAsyncEnumerable<ProgressLine> InstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var pipx = await ResolveAsync(ct);
        if (pipx is null) { yield return new ProgressLine("pipx nicht gefunden", true); yield break; }
        await foreach (var l in _runner.StreamAsync(pipx, new[] { "install", id }, ct)) yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UninstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var pipx = await ResolveAsync(ct);
        if (pipx is null) { yield return new ProgressLine("pipx nicht gefunden", true); yield break; }
        await foreach (var l in _runner.StreamAsync(pipx, new[] { "uninstall", id }, ct)) yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UpdateAsync(string? id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var pipx = await ResolveAsync(ct);
        if (pipx is null) { yield return new ProgressLine("pipx nicht gefunden", true); yield break; }
        // Ohne Id: alles aktualisieren. Mit Id: ein einzelnes venv.
        var args = id is null ? new[] { "upgrade-all" } : new[] { "upgrade", id };
        await foreach (var l in _runner.StreamAsync(pipx, args, ct)) yield return l;
    }
}
