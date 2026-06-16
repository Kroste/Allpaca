using System.Runtime.CompilerServices;
using System.Text.Json;
using Allpaca.Models;
using NLog;

namespace Allpaca.Services.Sources;

public sealed class HomebrewSource : IPackageSource
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    // brew steht in GUI-Sessions (KDE/Wayland) oft nicht im PATH – daher
    // zusaetzlich der bekannte Linuxbrew-Pfad als Fallback.
    private static readonly string[] Candidates =
    {
        "brew",
        "/home/linuxbrew/.linuxbrew/bin/brew",
    };

    private readonly ProcessRunner _runner;
    private string? _brew;

    public HomebrewSource(ProcessRunner runner) => _runner = runner;

    public PackageSourceKind Kind => PackageSourceKind.Homebrew;
    public string DisplayName => "Homebrew";
    public PackageCapabilities Capabilities => new()
    {
        CanSearch = true, CanInstall = true, CanUninstall = true, CanUpdate = true,
        RequiresRoot = false, RequiresReboot = false,
    };

    private async Task<string?> ResolveAsync(CancellationToken ct)
    {
        if (_brew is not null) return _brew;
        foreach (var c in Candidates)
        {
            if ((await _runner.RunAsync(c, new[] { "--version" }, ct)).Success)
            {
                _brew = c;
                Log.Info("brew gefunden: {0}", c);
                return _brew;
            }
        }
        return null;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => await ResolveAsync(ct) is not null;

    public async Task<IReadOnlyList<PackageInfo>> ListInstalledAsync(CancellationToken ct = default)
    {
        var brew = await ResolveAsync(ct);
        if (brew is null) return Array.Empty<PackageInfo>();

        var r = await _runner.RunAsync(brew, new[] { "info", "--json=v2", "--installed" }, ct);
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut))
        {
            Log.Warn("brew info fehlgeschlagen: {0}", r.StdErr);
            return Array.Empty<PackageInfo>();
        }

        var list = new List<PackageInfo>();
        using var doc = JsonDocument.Parse(r.StdOut);
        var root = doc.RootElement;

        if (root.TryGetProperty("formulae", out var formulae))
        {
            foreach (var f in formulae.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (name.Length == 0) continue;

                string? ver = null;
                if (f.TryGetProperty("installed", out var inst) && inst.GetArrayLength() > 0
                    && inst[0].TryGetProperty("version", out var v))
                    ver = v.GetString();

                list.Add(new PackageInfo
                {
                    Id = name,
                    Name = name,
                    Version = ver,
                    Source = Kind,
                    Description = f.TryGetProperty("desc", out var d) ? d.GetString() : null,
                    Origin = f.TryGetProperty("tap", out var t) ? t.GetString() : null,
                    Scope = "formula",
                });
            }
        }

        if (root.TryGetProperty("casks", out var casks))
        {
            foreach (var c in casks.EnumerateArray())
            {
                var token = c.TryGetProperty("token", out var tk) ? tk.GetString() ?? "" : "";
                if (token.Length == 0) continue;

                string display = token;
                if (c.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.Array
                    && nm.GetArrayLength() > 0)
                    display = nm[0].GetString() ?? token;

                list.Add(new PackageInfo
                {
                    Id = token,
                    Name = display,
                    Version = c.TryGetProperty("version", out var v) ? v.GetString() : null,
                    Source = Kind,
                    Description = c.TryGetProperty("desc", out var d) ? d.GetString() : null,
                    Origin = c.TryGetProperty("tap", out var t) ? t.GetString() : null,
                    Scope = "cask",
                });
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<PackageInfo>();
        var brew = await ResolveAsync(ct);
        if (brew is null) return Array.Empty<PackageInfo>();

        var r = await _runner.RunAsync(brew, new[] { "search", query }, ct);
        if (!r.Success)
        {
            Log.Warn("brew search '{0}' fehlgeschlagen: {1}", query, r.StdErr);
            return Array.Empty<PackageInfo>();
        }

        return BrewSearchParser.Parse(r.StdOut);
    }

    public async Task<IReadOnlySet<string>> CheckUpdatesAsync(CancellationToken ct = default)
    {
        var brew = await ResolveAsync(ct);
        if (brew is null) return new HashSet<string>();

        // brew outdated --json=v2 liefert ein Objekt mit "formulae" und "casks".
        var r = await _runner.RunAsync(brew, new[] { "outdated", "--json=v2" }, ct);
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut))
        {
            Log.Warn("brew outdated fehlgeschlagen: {0}", r.StdErr);
            return new HashSet<string>();
        }

        return HomebrewOutdatedParser.ParseIds(r.StdOut);
    }

    public async IAsyncEnumerable<ProgressLine> InstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var brew = await ResolveAsync(ct);
        if (brew is null) { yield return new ProgressLine("Homebrew nicht gefunden", true); yield break; }
        await foreach (var l in _runner.StreamAsync(brew, new[] { "install", id }, ct)) yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UninstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var brew = await ResolveAsync(ct);
        if (brew is null) { yield return new ProgressLine("Homebrew nicht gefunden", true); yield break; }
        await foreach (var l in _runner.StreamAsync(brew, new[] { "uninstall", id }, ct)) yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UpdateAsync(string? id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var brew = await ResolveAsync(ct);
        if (brew is null) { yield return new ProgressLine("Homebrew nicht gefunden", true); yield break; }
        var args = id is null ? new[] { "upgrade" } : new[] { "upgrade", id };
        await foreach (var l in _runner.StreamAsync(brew, args, ct)) yield return l;
    }

    // --- Native Batching: brew akzeptiert mehrere Tokens/Formulae in einem
    // Aufruf, das spart pro-Eintrag-Setup-Overhead (taps neu auswerten etc.). ---

    public async IAsyncEnumerable<ProgressLine> UninstallManyAsync(
        IReadOnlyList<string> ids,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (ids.Count == 0) yield break;
        var brew = await ResolveAsync(ct);
        if (brew is null) { yield return new ProgressLine("Homebrew nicht gefunden", true); yield break; }

        var args = new List<string> { "uninstall" };
        args.AddRange(ids);
        await foreach (var l in _runner.StreamAsync(brew, args, ct)) yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UpdateManyAsync(
        IReadOnlyList<string> ids,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (ids.Count == 0) yield break;
        var brew = await ResolveAsync(ct);
        if (brew is null) { yield return new ProgressLine("Homebrew nicht gefunden", true); yield break; }

        var args = new List<string> { "upgrade" };
        args.AddRange(ids);
        await foreach (var l in _runner.StreamAsync(brew, args, ct)) yield return l;
    }
}
