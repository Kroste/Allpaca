using System.Net.Http;
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
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
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
}
