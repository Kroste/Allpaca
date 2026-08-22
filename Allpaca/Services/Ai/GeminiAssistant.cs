using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using NLog;

namespace Allpaca.Services.Ai;

/// <summary>Gemini über die Generative-Language-API mit Streaming
/// (models/{model}:streamGenerateContent?alt=sse).</summary>
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

    public async IAsyncEnumerable<string> CompleteStreamAsync(
        string system, string user,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{_s.ResolvedEndpoint.TrimEnd('/')}/models/{_s.ResolvedModel}:streamGenerateContent?alt=sse&key={_s.ApiKey}";
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

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Log.Warn("Gemini HTTP {0}: {1}", (int)resp.StatusCode, err);
            throw new HttpRequestException($"Gemini-Antwort {(int)resp.StatusCode}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var data in SseStreamReader.ReadDataLinesAsync(stream, ct))
        {
            var chunk = GeminiStreamParser.ExtractContent(data);
            if (!string.IsNullOrEmpty(chunk)) yield return chunk;
        }
    }

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in CompleteStreamAsync(system, user, ct).ConfigureAwait(false))
            sb.Append(chunk);
        return sb.ToString();
    }
}
