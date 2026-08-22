using System.Text.Json;

namespace Allpaca.Services.Ai;

/// <summary>
/// Parser für einen "data:"-Block aus dem Gemini-Streaming-Endpoint
/// (POST :streamGenerateContent?alt=sse). Format:
///   {"candidates":[{"content":{"parts":[{"text":"…"}],"role":"model"}}]}
/// Manche Frames enthalten nur Metadaten ohne text - dann null zurück.
/// </summary>
internal static class GeminiStreamParser
{
    public static string? ExtractContent(string dataPayload)
    {
        if (string.IsNullOrWhiteSpace(dataPayload)) return null;

        try
        {
            using var doc = JsonDocument.Parse(dataPayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates)) return null;
            if (candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
                return null;

            if (!candidates[0].TryGetProperty("content", out var content)) return null;
            if (!content.TryGetProperty("parts", out var parts)) return null;
            if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
                return null;

            if (!parts[0].TryGetProperty("text", out var text)) return null;
            return text.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
