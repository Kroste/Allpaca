namespace Allpaca.Services.Ai;

/// <summary>
/// System- und User-Prompt fuer die natuerlichsprachige Such-Empfehlung im
/// SearchWindow. Output-Format ist HART vorgeschrieben, damit AiSuggestionParser
/// die Antwort zuverlaessig zerlegen kann.
/// </summary>
public static class AiSuggestionPromptBuilder
{
    public const string SystemPrompt =
        "Du bist Linux-Paket-Manager-Experte für Bazzite (Fedora Atomic). " +
        "Quellen für Installation: Flatpak (GUI-Apps bevorzugt) und Homebrew (CLI-Tools bevorzugt). " +
        "Der User beschreibt umgangssprachlich, was er installieren möchte. " +
        "Schlage konkrete, dir wirklich bekannte Pakete vor.\n\n" +
        "Antworte AUSSCHLIESSLICH in diesem Format, eine Zeile pro Vorschlag, max 5 Vorschläge:\n" +
        "PROVIDER|PAKET-ID|KURZE_BEGRÜNDUNG\n\n" +
        "PROVIDER = exakt \"Flatpak\" ODER \"Homebrew\".\n" +
        "PAKET-ID = exakte Flatpak-App-ID (z. B. org.gimp.GIMP) oder Homebrew-Formula/Cask-Token (z. B. ffmpeg).\n" +
        "KURZE_BEGRÜNDUNG = max 1 kurzer Satz auf Deutsch, warum dieses Paket passt.\n\n" +
        "Keine Markdown, kein Vorspann, keine Erklärung außerhalb der Zeilen. " +
        "Wenn du dir bei einer ID nicht sicher bist, lass sie weg. " +
        "Lieber 2 sichere Vorschläge als 5 ratende.";

    public static string BuildUserPrompt(string userQuery) =>
        $"Ich suche: {userQuery.Trim()}";
}
