using System.Text;
using Allpaca.Models;

namespace Allpaca.Services.Ai;

/// <summary>
/// Baut den Prompt für die KI-Fehlerdiagnose im LogWindow. Pur statisch, damit
/// die Logik (Truncation, Zeilenformat) testbar bleibt - die HTTP-Calls liegen
/// in AiAssistant.
/// </summary>
public static class DiagnosisPromptBuilder
{
    /// <summary>System-Prompt: Rolle + Format-Constraint. Identisch für alle Diagnosen.</summary>
    public const string SystemPrompt =
        "Du bist ein Linux-Paket-Manager-Experte für Bazzite (Fedora Atomic). " +
        "Du kennst Flatpak, Homebrew, rpm-ostree, Distrobox und AppImage. " +
        "Analysiere die folgende Fehlerausgabe einer Paket-Operation. " +
        "Gib eine kurze, klare Antwort auf Deutsch (max 8 Sätze, Klartext, kein Markdown): " +
        "1) Was ist das Problem in 1–2 Sätzen? " +
        "2) Was kann der User konkret tun – nenne, wenn sinnvoll, einen Terminal-Befehl in Backticks. " +
        "Nichts erfinden – wenn die Logs nicht reichen, sag das ehrlich.";

    /// <summary>Maximalanzahl Log-Zeilen, die wir an die KI weitergeben (Tail).</summary>
    public const int MaxLines = 50;

    /// <summary>Pro Log-Zeile maximal so viele Zeichen, um Token zu sparen.</summary>
    public const int MaxLineLength = 300;

    public static string BuildUserPrompt(string operationTitle, int? exitCode, IReadOnlyList<ProgressLine> lines)
    {
        var sb = new StringBuilder();
        sb.Append("Operation: ").AppendLine(operationTitle);
        if (exitCode is int code) sb.Append("Exit-Code: ").AppendLine(code.ToString());
        sb.AppendLine();

        // Nur die letzten MaxLines Zeilen, jede pro MaxLineLength gekappt.
        var tail = lines.Skip(Math.Max(0, lines.Count - MaxLines)).ToList();
        var skipped = lines.Count - tail.Count;
        if (skipped > 0)
            sb.Append("(").Append(skipped).AppendLine(" frühere Zeilen ausgelassen)");

        sb.AppendLine("Letzte Log-Zeilen:");
        foreach (var l in tail)
        {
            var text = l.Text.Length > MaxLineLength
                ? l.Text[..MaxLineLength] + "…"
                : l.Text;
            sb.Append(l.IsError ? "[ERR] " : "      ").AppendLine(text);
        }

        return sb.ToString();
    }
}
