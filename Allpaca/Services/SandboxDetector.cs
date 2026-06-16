namespace Allpaca.Services;

/// <summary>
/// Erkennt, ob Allpaca selbst in einer Sandbox laeuft. Relevant, weil die
/// Paket-Tools (flatpak, brew, rpm-ostree, distrobox) auf dem HOST liegen.
/// In einer Sandbox muessen Host-Kommandos ueber flatpak-spawn --host laufen.
/// </summary>
public sealed class SandboxDetector
{
    public enum SandboxKind { None, Flatpak, Container }

    public SandboxKind Kind { get; }
    public bool IsSandboxed => Kind != SandboxKind.None;

    public SandboxDetector()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")))
            Kind = SandboxKind.Flatpak;
        else if (File.Exists("/run/.containerenv")
                 || File.Exists("/.dockerenv")
                 || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_ID")))
            Kind = SandboxKind.Container;
        else
            Kind = SandboxKind.None;
    }
}
