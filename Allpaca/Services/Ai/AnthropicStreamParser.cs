using System.Text.Json;

namespace Allpaca.Services.Ai;

/// <summary>
/// Parser fuer einen "data:"-Block aus dem Anthropic-Streaming-Endpoint
/// (POST /v1/messages mit stream=true). Mehrere Event-Typen, wir interessieren
/// uns nur fuer "content_block_delta" mit "text_delta":
///   {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"…"}}
/// Alle anderen Events (message_start, content_block_start, message_stop, …)
/// liefern null und werden vom Caller stillschweigend uebersprungen.
/// </summary>
internal static class AnthropicStreamParser
{
    public static string? ExtractContent(string dataPayload)
    {
        if (string.IsNullOrWhiteSpace(dataPayload)) return null;

        try
        {
            using var doc = JsonDocument.Parse(dataPayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var type) ||
                type.GetString() != "content_block_delta")
                return null;

            if (!root.TryGetProperty("delta", out var delta)) return null;
            if (!delta.TryGetProperty("type", out var dtype) ||
                dtype.GetString() != "text_delta")
                return null;

            if (!delta.TryGetProperty("text", out var text)) return null;
            return text.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
