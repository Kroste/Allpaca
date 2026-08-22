using System.IO;
using System.Runtime.CompilerServices;

namespace Allpaca.Services.Ai;

/// <summary>
/// Gemeinsamer Reader für Server-Sent-Events von HTTP-Response-Streams. Yieldet
/// pro "data: …"-Zeile den JSON-Payload (ohne den "data: "-Prefix). Andere Zeilen
/// (event:, leer, Kommentare mit ":" Präfix) überspringt der Reader still.
/// </summary>
internal static class SseStreamReader
{
    public static async IAsyncEnumerable<string> ReadDataLinesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (line.StartsWith("data: ", System.StringComparison.Ordinal))
                yield return line[6..];
            // alles andere ignorieren: event:-Zeilen, leere Zeilen zwischen Events,
            // Kommentar-Zeilen mit ":"-Präfix.
        }
    }
}
