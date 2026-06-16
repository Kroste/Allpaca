using System.Net.Http;
using System.Text;
using System.Text.Json;
using NLog;

namespace Allpaca.Services.Ai;

/// <summary>Claude ueber die native Messages-API (/v1/messages).</summary>
public sealed class AnthropicAssistant : IAiAssistant
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly AiSettings _s;
    private readonly HttpClient _http;

    public AnthropicAssistant(AiSettings settings, HttpClient http)
    {
        _s = settings;
        _http = http;
    }

    public AiProvider Provider => AiProvider.Anthropic;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_s.ApiKey);

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var url = _s.ResolvedEndpoint.TrimEnd('/') + "/messages";
        var payload = new
        {
            model = _s.ResolvedModel,
            max_tokens = 1024,
            system,
            messages = new[] { new { role = "user", content = user } },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-api-key", _s.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            Log.Warn("Claude HTTP {0}: {1}", (int)resp.StatusCode, body);
            throw new HttpRequestException($"Claude-Antwort {(int)resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
