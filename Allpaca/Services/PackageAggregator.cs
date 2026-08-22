using Allpaca.Models;
using NLog;

namespace Allpaca.Services;

public sealed record SourceLoadResult(
    IPackageSource Source,
    bool Available,
    IReadOnlyList<PackageInfo> Packages,
    string? Error);

/// <summary>Bündelt alle Quellen und lädt jede einzeln, fehlertolerant.</summary>
public sealed class PackageAggregator
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public IReadOnlyList<IPackageSource> Sources { get; }

    public PackageAggregator(IReadOnlyList<IPackageSource> sources) => Sources = sources;

    public async Task<SourceLoadResult> LoadOneAsync(IPackageSource s, CancellationToken ct = default)
    {
        try
        {
            if (!await s.IsAvailableAsync(ct))
            {
                Log.Info("{0}: nicht verfügbar", s.DisplayName);
                return new SourceLoadResult(s, false, Array.Empty<PackageInfo>(), "nicht verfügbar");
            }

            var pkgs = await s.ListInstalledAsync(ct);
            Log.Info("{0}: {1} Einträge", s.DisplayName, pkgs.Count);
            return new SourceLoadResult(s, true, pkgs, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Laden fehlgeschlagen: {0}", s.DisplayName);
            return new SourceLoadResult(s, false, Array.Empty<PackageInfo>(), ex.Message);
        }
    }
}
