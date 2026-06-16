using System.Runtime.CompilerServices;
using System.Text.Json;
using Allpaca.Models;
using NLog;

namespace Allpaca.Services.Sources;

public sealed class RpmOstreeSource : IPackageSource
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly ProcessRunner _runner;

    public RpmOstreeSource(ProcessRunner runner) => _runner = runner;

    public PackageSourceKind Kind => PackageSourceKind.RpmOstree;
    public string DisplayName => "rpm-ostree (Layer)";
    public PackageCapabilities Capabilities => new()
    {
        CanSearch = false, CanInstall = true, CanUninstall = true, CanUpdate = true,
        RequiresRoot = true, RequiresReboot = true,
    };

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => (await _runner.RunAsync("rpm-ostree", new[] { "--version" }, ct)).Success;

    public async Task<IReadOnlyList<PackageInfo>> ListInstalledAsync(CancellationToken ct = default)
    {
        var r = await _runner.RunAsync("rpm-ostree", new[] { "status", "--json" }, ct);
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut))
        {
            Log.Warn("rpm-ostree status fehlgeschlagen: {0}", r.StdErr);
            return Array.Empty<PackageInfo>();
        }

        var list = new List<PackageInfo>();
        using var doc = JsonDocument.Parse(r.StdOut);
        if (!doc.RootElement.TryGetProperty("deployments", out var deps) || deps.GetArrayLength() == 0)
            return list;

        // Gebootetes Deployment bevorzugen, sonst das erste.
        var booted = deps[0];
        foreach (var d in deps.EnumerateArray())
        {
            if (d.TryGetProperty("booted", out var b) && b.ValueKind == JsonValueKind.True)
            {
                booted = d;
                break;
            }
        }

        // "packages" = tatsaechlich gelayerte Pakete; Fallback "requested-packages".
        if (!TryReadStringArray(booted, "packages", list)
            && !TryReadStringArray(booted, "requested-packages", list))
        {
            Log.Info("rpm-ostree: keine gelayerten Pakete");
        }

        return list;
    }

    private bool TryReadStringArray(JsonElement deployment, string prop, List<PackageInfo> sink)
    {
        if (!deployment.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return false;
        if (arr.GetArrayLength() == 0)
            return false;

        foreach (var p in arr.EnumerateArray())
        {
            var name = p.GetString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            sink.Add(new PackageInfo { Id = name, Name = name, Source = Kind, Scope = "layered" });
        }
        return true;
    }

    public Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PackageInfo>>(Array.Empty<PackageInfo>());

    // Mutationen brauchen pkexec; UI-Verdrahtung folgt in v2.
    public async IAsyncEnumerable<ProgressLine> InstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var l in _runner.StreamAsync("pkexec", new[] { "rpm-ostree", "install", id }, ct))
            yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UninstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var l in _runner.StreamAsync("pkexec", new[] { "rpm-ostree", "uninstall", id }, ct))
            yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UpdateAsync(string? id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var l in _runner.StreamAsync("pkexec", new[] { "rpm-ostree", "upgrade" }, ct))
            yield return l;
    }
}
