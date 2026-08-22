using System.Text.Json;
using Allpaca.Models;

namespace Allpaca.Services.Sources;

/// <summary>
/// Parser für "pipx list --json". Struktur (vereinfacht):
/// {
///   "venvs": {
///     "youtube-dl": {
///       "metadata": {
///         "main_package": {
///           "package": "youtube-dl",
///           "package_version": "2021.12.17"
///         }
///       }
///     }
///   }
/// }
/// </summary>
internal static class PipxListParser
{
    public static IReadOnlyList<PackageInfo> Parse(string json)
    {
        var list = new List<PackageInfo>();
        if (string.IsNullOrWhiteSpace(json)) return list;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("venvs", out var venvs)) return list;
        if (venvs.ValueKind != JsonValueKind.Object) return list;

        foreach (var venv in venvs.EnumerateObject())
        {
            var venvName = venv.Name;
            if (string.IsNullOrWhiteSpace(venvName)) continue;

            string? pkg = null;
            string? version = null;

            if (venv.Value.TryGetProperty("metadata", out var meta)
                && meta.TryGetProperty("main_package", out var main))
            {
                if (main.TryGetProperty("package", out var p) && p.GetString() is { Length: > 0 } pn)
                    pkg = pn;
                if (main.TryGetProperty("package_version", out var v) && v.GetString() is { Length: > 0 } pv)
                    version = pv;
            }

            // Fallback: wenn metadata fehlt, nehmen wir den venv-Schlüssel als Id+Name.
            var id = pkg ?? venvName;
            list.Add(new PackageInfo
            {
                Id = id,
                Name = id,
                Version = version,
                Source = PackageSourceKind.Pipx,
                Scope = "user",
            });
        }
        return list;
    }
}
