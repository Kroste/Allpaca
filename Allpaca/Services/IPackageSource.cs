using Allpaca.Models;

namespace Allpaca.Services;

/// <summary>
/// Abstraktion ueber eine Installationsquelle. v1 nutzt nur die Lese-Pfade
/// (IsAvailable + ListInstalled). Die Mutations-Signaturen sind bereits
/// definiert, damit die UI in v2 ohne Architekturbruch andocken kann.
/// </summary>
public interface IPackageSource
{
    PackageSourceKind Kind { get; }
    string DisplayName { get; }
    PackageCapabilities Capabilities { get; }

    /// <summary>Ist das zugehoerige Tool auf dem Host vorhanden/aufrufbar?</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Alle installierten Eintraege dieser Quelle.</summary>
    Task<IReadOnlyList<PackageInfo>> ListInstalledAsync(CancellationToken ct = default);

    // --- v2 ---
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken ct = default);
    IAsyncEnumerable<ProgressLine> InstallAsync(string id, CancellationToken ct = default);
    IAsyncEnumerable<ProgressLine> UninstallAsync(string id, CancellationToken ct = default);
    IAsyncEnumerable<ProgressLine> UpdateAsync(string? id, CancellationToken ct = default);

    /// <summary>
    /// Liefert die IDs aller eigenen Eintraege, fuer die ein Update verfuegbar ist.
    /// Default: leeres Set - Quellen, die das nicht unterstuetzen (z. B. AppImage,
    /// Distrobox-Container), brauchen nichts zu ueberschreiben.
    /// </summary>
    Task<IReadOnlySet<string>> CheckUpdatesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>
    /// Deinstalliert mehrere Eintraege in einer Operation. Default: sequentiell
    /// einzeln, damit der Stream nur ein Fenster braucht. Quellen mit nativer
    /// Batch-CLI (z. B. Flatpak) ueberschreiben das fuer Geschwindigkeit.
    /// </summary>
    async IAsyncEnumerable<ProgressLine> UninstallManyAsync(
        IReadOnlyList<string> ids,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            yield return new ProgressLine($"--- Deinstalliere {id} ---", false);
            await foreach (var l in UninstallAsync(id, ct))
                yield return l;
        }
    }

    /// <summary>Analog zu <see cref="UninstallManyAsync"/> fuer Updates.</summary>
    async IAsyncEnumerable<ProgressLine> UpdateManyAsync(
        IReadOnlyList<string> ids,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            yield return new ProgressLine($"--- Aktualisiere {id} ---", false);
            await foreach (var l in UpdateAsync(id, ct))
                yield return l;
        }
    }
}
