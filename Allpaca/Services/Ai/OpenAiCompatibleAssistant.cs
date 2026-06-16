using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NLog;

namespace Allpaca.Services.Ai;

/// <summary>
/// Deckt Ollama UND OpenAI/ChatGPT ab – beide nutzen POST {endpoint}/chat/completions.
/// Ollama braucht keinen Key; OpenAI erwartet einen Bearer-Token.
/// </summary>
public sealed class OpenAiCompatibleAssistant : IAiAssistant
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly AiSettings _s;
    private readonly HttpClient _http;

    public OpenAiCompatibleAssistant(AiSettings settings, HttpClient http)
    {
        _s = settings;
        _http = http;
    }

    public AiProvider Provider => _s.Provider;

    // Ollama braucht keinen Key; OpenAI schon.
    public bool IsConfigured => _s.Provider == AiProvider.Ollama || !string.IsNullOrWhiteSpace(_s.ApiKey);

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var url = _s.ResolvedEndpoint.TrimEnd('/') + "/chat/completions";
        var payload = new
        {
            model = _s.ResolvedModel,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
            stream = false,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_s.ApiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _s.ApiKey);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            Log.Warn("KI ({0}) HTTP {1}: {2}", _s.Provider, (int)resp.StatusCode, body);
            throw new HttpRequestException($"KI-Antwort {(int)resp.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
