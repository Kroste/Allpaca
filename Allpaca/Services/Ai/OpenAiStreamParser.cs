using System.Text.Json;

namespace Allpaca.Services.Ai;

/// <summary>
/// Parser fuer einen "data:"-Block aus dem OpenAI/Ollama-Streaming-Endpoint
/// (POST /chat/completions mit stream=true). Format pro Event:
///   {"choices":[{"delta":{"content":"…"}, "finish_reason":null}], ...}
/// Erstes/letztes Event enthaelt oft kein "content" - dann null zurueck.
/// </summary>
internal static class OpenAiStreamParser
{
    /// <summary>Sentinel-Wert, den OpenAI/Ollama am Ende des Streams senden.</summary>
    public const string DoneSentinel = "[DONE]";

    public static string? ExtractContent(string dataPayload)
    {
        if (string.IsNullOrWhiteSpace(dataPayload)) return null;
        if (dataPayload == DoneSentinel) return null;

        try
        {
            using var doc = JsonDocument.Parse(dataPayload);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)) return null;
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return null;
            if (!choices[0].TryGetProperty("delta", out var delta)) return null;
            if (!delta.TryGetProperty("content", out var content)) return null;
            return content.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
