using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using NLog;

namespace Allpaca.Services.Ai;

/// <summary>
/// Liest die Liste der lokal installierten Ollama-Modelle. Akzeptiert beide Endpoint-
/// Varianten, die in AiSettings auftauchen koennen: das OpenAI-kompatible "/v1"-Suffix
/// und den nativen Ollama-Pfad ohne /v1.
/// </summary>
public sealed class OllamaModelService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly HttpClient _http;

    public OllamaModelService(HttpClient? http = null)
    {
        // Lange Timeouts fuer Pull (mehrere GB Download) - die einzelne Streaming-Pause
        // soll nicht versehentlich den Pull abreissen.
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
    }

    public async Task<IReadOnlyList<string>> ListLocalAsync(
        string endpoint, CancellationToken ct = default)
    {
        var url = $"{NormalizeApiBase(endpoint)}/api/tags";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return OllamaTagsParser.ParseNames(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn(ex, "Ollama /api/tags fehlgeschlagen ({0})", url);
            throw;
        }
    }

    /// <summary>Aus "http://localhost:11434/v1" wird "http://localhost:11434".</summary>
    internal static string NormalizeApiBase(string endpoint)
    {
        var e = endpoint.TrimEnd('/');
        if (e.EndsWith("/v1", StringComparison.Ordinal))
            e = e[..^3];
        return e;
    }

    /// <summary>
    /// Streamt die NDJSON-Events von POST /api/pull. Ein Event pro Status-Wechsel
    /// (manifest, downloading-Fortschritt, verifying, writing, success). Cancel
    /// schliesst die Verbindung sauber und bricht den Server-seitigen Pull mit ab.
    /// </summary>
    public async IAsyncEnumerable<OllamaPullEvent> PullAsync(
        string endpoint, string model,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{NormalizeApiBase(endpoint)}/api/pull";
        var body = JsonSerializer.Serialize(new { name = model });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            yield return new OllamaPullEvent(
                Status: "error", Completed: null, Total: null, Digest: null,
                IsError: true, ErrorMessage: $"HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}");
            yield break;
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            var evt = OllamaPullProgressParser.ParseLine(line);
            if (evt is not null) yield return evt;
        }
    }
}
