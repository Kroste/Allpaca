using System.Globalization;
using System.Runtime.CompilerServices;
using Allpaca.Models;
using Allpaca.Services;
using NLog;

namespace Allpaca.Services.Sources;

public sealed class FlatpakSource : IPackageSource
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly ProcessRunner _runner;

    public FlatpakSource(ProcessRunner runner) => _runner = runner;

    public PackageSourceKind Kind => PackageSourceKind.Flatpak;
    public string DisplayName => "Flatpak";
    public PackageCapabilities Capabilities => new()
    {
        CanSearch = true, CanInstall = true, CanUninstall = true, CanUpdate = true,
        RequiresRoot = false, RequiresReboot = false,
    };

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => (await _runner.RunAsync("flatpak", new[] { "--version" }, ct)).Success;

    public async Task<IReadOnlyList<PackageInfo>> ListInstalledAsync(CancellationToken ct = default)
    {
        // Apps und Runtimes getrennt holen - die UI blendet Runtimes per Default aus.
        var apps = await ListAsync(asRuntime: false, ct).ConfigureAwait(false);
        var runtimes = await ListAsync(asRuntime: true, ct).ConfigureAwait(false);
        return apps.Concat(runtimes).ToList();
    }

    private async Task<List<PackageInfo>> ListAsync(bool asRuntime, CancellationToken ct)
    {
        // Tab-getrennte, stabil parsbare Spalten.
        const string cols = "application,name,version,branch,origin,installation,size";
        var modeArg = asRuntime ? "--runtime" : "--app";
        var r = await _runner.RunAsync("flatpak",
            new[] { "list", modeArg, "--columns=" + cols }, ct);

        if (!r.Success)
        {
            Log.Warn("flatpak list {0} fehlgeschlagen: {1}", modeArg, r.StdErr);
            return new List<PackageInfo>();
        }

        var list = new List<PackageInfo>();
        foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split('\t');
            if (p.Length < 7) continue;

            var id = p[0].Trim();
            list.Add(new PackageInfo
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(p[1]) ? id : p[1].Trim(),
                Version = string.IsNullOrWhiteSpace(p[2]) ? null : p[2].Trim(),
                Source = Kind,
                Origin = p[4].Trim(),
                Scope = p[5].Trim(),                 // user / system
                SizeBytes = ParseSize(p[6].Trim()),
                IsRuntime = asRuntime,
                // Flatpak exportiert seine App-Icons in hicolor unter dem App-ID-
                // Schluessel. Runtimes haben keine Icons, deshalb spaaren wir uns die
                // Disk-Suche dafuer.
                IconPath = asRuntime ? null : IconLookup.FindPng(id),
                Extra = new Dictionary<string, string> { ["branch"] = p[3].Trim() },
            });
        }
        return list;
    }

    /// <summary>Parst Flatpaks Groessen-Spalte ("1,2 GB", "234 MB", de/en-Locale).</summary>
    internal static long? ParseSize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Replace(',', '.').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return null;

        double mult = parts[1].ToLowerInvariant() switch
        {
            "b" => 1d,
            "kb" => 1024d,
            "mb" => 1024d * 1024,
            "gb" => 1024d * 1024 * 1024,
            "tb" => 1024d * 1024 * 1024 * 1024,
            _ => 1d,
        };
        return (long)(num * mult);
    }

    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<PackageInfo>();

        var r = await _runner.RunAsync("flatpak",
            new[] { "search", "--columns=application,name,description,branch,remotes", query }, ct);

        if (!r.Success)
        {
            Log.Warn("flatpak search '{0}' fehlgeschlagen: {1}", query, r.StdErr);
            return Array.Empty<PackageInfo>();
        }

        return FlatpakSearchParser.Parse(r.StdOut);
    }

    public async Task<IReadOnlySet<string>> CheckUpdatesAsync(CancellationToken ct = default)
    {
        // "flatpak remote-ls --updates" listet alles, was an verfuegbaren Updates auf den
        // konfigurierten Remotes vorliegt. --columns=application reduziert auf die App-ID
        // (eine pro Zeile, ohne Header).
        var r = await _runner.RunAsync("flatpak",
            new[] { "remote-ls", "--updates", "--columns=application" }, ct);

        if (!r.Success)
        {
            Log.Warn("flatpak remote-ls --updates fehlgeschlagen: {0}", r.StdErr);
            return new HashSet<string>();
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var id = raw.Trim();
            if (id.Length > 0) set.Add(id);
        }
        return set;
    }

    public async IAsyncEnumerable<ProgressLine> InstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var l in _runner.StreamAsync("flatpak", new[] { "install", "-y", id }, ct))
            yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UninstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var l in _runner.StreamAsync("flatpak", new[] { "uninstall", "-y", id }, ct))
            yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UpdateAsync(string? id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var args = id is null ? new[] { "update", "-y" } : new[] { "update", "-y", id };
        await foreach (var l in _runner.StreamAsync("flatpak", args, ct))
            yield return l;
    }

    // --- Native Batching: flatpak nimmt mehrere IDs in einem Aufruf entgegen,
    // das ist deutlich schneller als sequentielles Iterieren. ---

    public async IAsyncEnumerable<ProgressLine> UninstallManyAsync(
        IReadOnlyList<string> ids,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (ids.Count == 0) yield break;
        var args = new List<string> { "uninstall", "-y" };
        args.AddRange(ids);
        await foreach (var l in _runner.StreamAsync("flatpak", args, ct))
            yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UpdateManyAsync(
        IReadOnlyList<string> ids,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (ids.Count == 0) yield break;
        var args = new List<string> { "update", "-y" };
        args.AddRange(ids);
        await foreach (var l in _runner.StreamAsync("flatpak", args, ct))
            yield return l;
    }
}
