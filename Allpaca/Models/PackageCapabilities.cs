namespace Allpaca.Models;

/// <summary>Was eine Quelle kann – steuert später (v2) die UI-Buttons.</summary>
public sealed record PackageCapabilities
{
    public bool CanSearch { get; init; }
    public bool CanInstall { get; init; }
    public bool CanUninstall { get; init; }
    public bool CanUpdate { get; init; }

    /// <summary>Mutationen erfordern Elevation (pkexec).</summary>
    public bool RequiresRoot { get; init; }

    /// <summary>Änderung wird erst nach Reboot wirksam (rpm-ostree).</summary>
    public bool RequiresReboot { get; init; }
}
