using System.Runtime.CompilerServices;
using Allpaca.Models;
using NLog;

namespace Allpaca.Services.Sources;

/// <summary>
/// Listet Distrobox-Container als Eintraege. Das granulare Auflisten der
/// Pakete *innerhalb* jedes Containers (enter + pm list) ist teuer und folgt
/// als Drill-down in v2.
/// </summary>
public sealed class DistroboxSource : IPackageSource
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly ProcessRunner _runner;

    public DistroboxSource(ProcessRunner runner) => _runner = runner;

    public PackageSourceKind Kind => PackageSourceKind.Distrobox;
    public string DisplayName => "Distrobox";
    public PackageCapabilities Capabilities => new()
    {
        CanSearch = false, CanInstall = false, CanUninstall = true, CanUpdate = true,
        RequiresRoot = false, RequiresReboot = false,
    };

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => (await _runner.RunAsync("distrobox", new[] { "version" }, ct)).Success;

    public async Task<IReadOnlyList<PackageInfo>> ListInstalledAsync(CancellationToken ct = default)
    {
        var r = await _runner.RunAsync("distrobox", new[] { "list", "--no-color" }, ct);
        if (!r.Success)
        {
            Log.Warn("distrobox list fehlgeschlagen: {0}", r.StdErr);
            return Array.Empty<PackageInfo>();
        }

        var list = new List<PackageInfo>();
        // Tabelle: "ID | NAME | STATUS | IMAGE" (erste Zeile = Header)
        foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var cols = line.Split('|');
            if (cols.Length < 4) continue;

            var cid = cols[0].Trim();
            var name = cols[1].Trim();
            var status = cols[2].Trim();
            var image = cols[3].Trim();
            if (name.Length == 0) continue;

            list.Add(new PackageInfo
            {
                Id = name,
                Name = name,
                Source = Kind,
                Description = image,
                Origin = image,
                Scope = "container",
                Extra = new Dictionary<string, string>
                {
                    ["status"] = status,
                    ["container-id"] = cid,
                },
            });
        }
        return list;
    }

    public Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PackageInfo>>(Array.Empty<PackageInfo>());

    public IAsyncEnumerable<ProgressLine> InstallAsync(string id, CancellationToken ct = default)
        => EmptyStream();

    public async IAsyncEnumerable<ProgressLine> UninstallAsync(string id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var l in _runner.StreamAsync("distrobox", new[] { "rm", "-f", id }, ct))
            yield return l;
    }

    public async IAsyncEnumerable<ProgressLine> UpdateAsync(string? id, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var args = id is null ? new[] { "upgrade", "--all" } : new[] { "upgrade", id };
        await foreach (var l in _runner.StreamAsync("distrobox", args, ct))
            yield return l;
    }

    private static async IAsyncEnumerable<ProgressLine> EmptyStream()
    {
        await Task.CompletedTask;
        yield break;
    }
}
