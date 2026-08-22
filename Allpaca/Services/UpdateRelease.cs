using System.Text.Json;

namespace Allpaca.Services;

/// <summary>Ein Release-Asset aus der GitHub-API (Name + Download-URL + Größe).</summary>
public sealed record UpdateAsset(string Name, string DownloadUrl, long Size);

/// <summary>Das neueste Release samt der Assets, die zu dieser Plattform passen.</summary>
public sealed record UpdateRelease(string TagName, string Version, string HtmlUrl, IReadOnlyList<UpdateAsset> Assets);

/// <summary>
/// Reine Parse- und Auswahllogik rund um GitHub-Releases -- bewusst ohne HTTP und
/// ohne Dateisystem, damit sie testbar bleibt.
/// </summary>
public static class UpdateReleaseParser
{
    /// <summary>Liest das JSON von <c>/releases/latest</c>. Gibt null zurück, wenn das
    /// JSON unbrauchbar ist -- ein kaputter Update-Check darf die App nie stören.</summary>
    public static UpdateRelease? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag)) return null;

            var html = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

            var assets = new List<UpdateAsset>();
            if (root.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in arr.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                    var size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0;
                    assets.Add(new UpdateAsset(name, url, size));
                }
            }

            return new UpdateRelease(tag, NormalizeVersion(tag), html, assets);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Schneidet das Tag-Präfix "v" und etwaige Build-Metadaten ab.</summary>
    public static string NormalizeVersion(string raw)
    {
        var s = raw.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        var plus = s.IndexOf('+');
        if (plus > 0) s = s[..plus];
        return s;
    }

    /// <summary>
    /// True, wenn <paramref name="candidate"/> semantisch neuer ist als
    /// <paramref name="current"/>. Stringvergleich wäre falsch: "1.10.0" ist
    /// größer als "1.9.0", sortiert sich als Text aber davor.
    /// </summary>
    public static bool IsNewer(string current, string candidate)
    {
        if (!TryParse(current, out var cur) || !TryParse(candidate, out var cand)) return false;
        return cand > cur;
    }

    private static bool TryParse(string raw, out Version version)
    {
        version = new Version(0, 0, 0);
        var s = NormalizeVersion(raw);
        // Vorabversionen ("1.6.0-rc.1") gelten für den Vergleich als ihre Basisversion.
        var dash = s.IndexOf('-');
        if (dash > 0) s = s[..dash];
        return Version.TryParse(s, out version!);
    }

    /// <summary>
    /// Wählt das Asset für die laufende Installationsform. Allpaca ist Linux-only,
    /// es gibt also genau zwei Kandidaten: das AppImage (wenn die App als AppImage
    /// läuft) und sonst den tar.gz-Tarball.
    /// </summary>
    public static UpdateAsset? SelectAsset(IReadOnlyList<UpdateAsset> assets, bool runningAsAppImage)
    {
        if (assets.Count == 0) return null;

        return runningAsAppImage
            ? assets.FirstOrDefault(a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
            : assets.FirstOrDefault(a => a.Name.EndsWith("linux-x64.tar.gz", StringComparison.OrdinalIgnoreCase))
              ?? assets.FirstOrDefault(a => a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));
    }
}
