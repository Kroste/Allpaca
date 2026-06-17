using System.Text.Json;

namespace Allpaca.Services.Ai;

/// <summary>
/// Parst eine Zeile aus Ollamas NDJSON-Stream von POST /api/pull. Jede Zeile ist ein
/// eigenstaendiges JSON-Objekt. Unbekannte Formate werden als null zurueckgegeben,
/// damit der Aufrufer schlicht ueberspringt.
/// </summary>
internal static class OllamaPullProgressParser
{
    public static OllamaPullEvent? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Fehlerantwort: { "error": "..." }
            if (root.TryGetProperty("error", out var errEl) && errEl.GetString() is { Length: > 0 } err)
                return new OllamaPullEvent(
                    Status: "error",
                    Completed: null,
                    Total: null,
                    Digest: null,
                    IsError: true,
                    ErrorMessage: err);

            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            long? completed = root.TryGetProperty("completed", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetInt64() : null;
            long? total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number
                ? t.GetInt64() : null;
            var digest = root.TryGetProperty("digest", out var d) ? d.GetString() : null;

            return new OllamaPullEvent(status, completed, total, digest);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
