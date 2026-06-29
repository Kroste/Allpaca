using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using NLog;

namespace Allpaca.Services.Ai;

/// <summary>
/// Deckt Ollama UND OpenAI/ChatGPT ab – beide nutzen POST {endpoint}/chat/completions
/// mit demselben SSE-Streaming-Format ("data: {…}" pro Chunk, Schluss-Sentinel
/// "data: [DONE]"). Ollama braucht keinen Key; OpenAI erwartet einen Bearer-Token.
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

    public async IAsyncEnumerable<string> CompleteStreamAsync(
        string system, string user,
        [EnumeratorCancellation] CancellationToken ct = default)
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
            stream = true,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_s.ApiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _s.ApiKey);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Log.Warn("KI ({0}) HTTP {1}: {2}", _s.Provider, (int)resp.StatusCode, err);
            throw new HttpRequestException($"KI-Antwort {(int)resp.StatusCode}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var data in SseStreamReader.ReadDataLinesAsync(stream, ct))
        {
            if (data == OpenAiStreamParser.DoneSentinel) yield break;
            var chunk = OpenAiStreamParser.ExtractContent(data);
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
