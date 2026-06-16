using System.Net.Http;
using System.Text;
using System.Text.Json;
using NLog;

namespace Allpaca.Services.Ai;

/// <summary>Gemini ueber die Generative-Language-API (models/{model}:generateContent).</summary>
public sealed class GeminiAssistant : IAiAssistant
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly AiSettings _s;
    private readonly HttpClient _http;

    public GeminiAssistant(AiSettings settings, HttpClient http)
    {
        _s = settings;
        _http = http;
    }

    public AiProvider Provider => AiProvider.Gemini;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_s.ApiKey);

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var url = $"{_s.ResolvedEndpoint.TrimEnd('/')}/models/{_s.ResolvedModel}:generateContent?key={_s.ApiKey}";
        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = system } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = user } } },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            Log.Warn("Gemini HTTP {0}: {1}", (int)resp.StatusCode, body);
            throw new HttpRequestException($"Gemini-Antwort {(int)resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}
