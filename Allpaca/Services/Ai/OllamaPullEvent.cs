namespace Allpaca.Services.Ai;

/// <summary>
/// Ein einzelnes Event aus Ollamas NDJSON-Antwort von POST /api/pull. Status beschreibt
/// die aktuelle Phase ("pulling manifest", "downloading", "verifying sha256 digest",
/// "writing manifest", "success"). Bytes-Felder sind nur waehrend "downloading" gesetzt;
/// das LogWindow verteilt sie auf die Progress-Bar.
/// </summary>
public sealed record OllamaPullEvent(
    string Status,
    long? Completed,
    long? Total,
    string? Digest,
    bool IsError = false,
    string? ErrorMessage = null);
