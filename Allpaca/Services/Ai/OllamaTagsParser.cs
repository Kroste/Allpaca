using System.Text.Json;

namespace Allpaca.Services.Ai;

/// <summary>
/// Parser fuer Ollamas GET /api/tags. Antwort sieht so aus:
/// { "models": [ { "name": "qwen2.5-coder:14b", "size": ..., ... }, ... ] }
/// Wir interessieren uns nur fuer die Namen.
/// </summary>
internal static class OllamaTagsParser
{
    public static IReadOnlyList<string> ParseNames(string json)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return list;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("models", out var arr)) return list;
        if (arr.ValueKind != JsonValueKind.Array) return list;

        foreach (var m in arr.EnumerateArray())
        {
            if (m.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name)
                list.Add(name);
        }
        return list;
    }
}
