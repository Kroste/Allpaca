using Allpaca.Models;

namespace Allpaca.Services.Sources;

/// <summary>
/// Parser fuer die Tab-getrennte Ausgabe unseres Container-Probe-Bash-Scripts
/// (siehe DistroboxSource.ListContainerPackagesAsync). Ein Eintrag pro Zeile,
/// Spalten "Name\tVersion".
/// </summary>
internal static class ContainerPackageParser
{
    public static IReadOnlyList<ContainerPackage> Parse(string stdout)
    {
        var list = new List<ContainerPackage>();
        if (string.IsNullOrWhiteSpace(stdout)) return list;

        foreach (var raw in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;

            var parts = line.Split('\t');
            var name = parts[0].Trim();
            if (name.Length == 0) continue;

            var version = parts.Length > 1 ? parts[1].Trim() : "";
            list.Add(new ContainerPackage(name, version));
        }
        return list;
    }
}
