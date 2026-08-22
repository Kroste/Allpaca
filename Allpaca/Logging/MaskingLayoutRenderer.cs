using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NLog.LayoutRenderers;

namespace Allpaca.Logging;

/// <summary>
/// NLog-Layout-Renderer <c>${masked:...}</c>: schreibt die Log-Message, ersetzt aber
/// vorher alles, was nach Secret aussieht. Allpaca redet mit vier KI-Providern und
/// reicht deren API-Keys durch HTTP-Header und URLs -- ohne diesen Filter landet ein
/// Key beim ersten Fehler im Klartext im Logfile.
/// </summary>
/// <remarks>
/// Muss über <c>LogManager.Setup().SetupExtensions(...)</c> registriert werden, BEVOR
/// der erste Logger benutzt wird. Sonst greift der Renderer nicht und NLog schluckt
/// das Message-Ende -- im Log steht dann nur noch die schließende Klammer.
/// </remarks>
[LayoutRenderer("masked")]
public sealed class MaskingLayoutRenderer : LayoutRenderer
{
    private const string Mask = "***";

    // Nach Provider-Konvention: sk-… (OpenAI), sk-ant-… (Anthropic), AIza… (Google),
    // dazu die üblichen key=/token=/authorization-Formen aus URLs und Headern.
    private static readonly Regex[] Patterns =
    [
        new(@"(?i)\b(sk-ant-[A-Za-z0-9_\-]{8,})", RegexOptions.Compiled),
        new(@"(?i)\b(sk-[A-Za-z0-9_\-]{16,})", RegexOptions.Compiled),
        new(@"\b(AIza[A-Za-z0-9_\-]{16,})", RegexOptions.Compiled),
        new(@"(?i)((?:api[_\-]?key|access[_\-]?token|token|password|passwd|secret)\s*[=:]\s*)([^\s&""',;]+)", RegexOptions.Compiled),
        new(@"(?i)(x-api-key\s*:\s*)([^\s]+)", RegexOptions.Compiled),
        new(@"(?i)(Authorization\s*:\s*Bearer\s+)([^\s]+)", RegexOptions.Compiled),
        new(@"(?i)([?&]key=)([^\s&]+)", RegexOptions.Compiled),
    ];

    /// <summary>
    /// Registriert den Renderer beim Laden des Assemblys -- also garantiert, bevor
    /// irgendwo der erste Logger gezogen wird.
    /// </summary>
    /// <remarks>
    /// Der Modul-Initializer statt eines Aufrufs in Program.Main ist Absicht: kennt
    /// NLog das ${masked} im Layout nicht, verschluckt es den REST DER ZEILE und im
    /// Log steht eine leere Message. Genau das ist hier passiert -- in der App lief
    /// die Registrierung in Main, im Testprozess (kein Main) nicht, und die
    /// Test-Logzeilen kamen ohne Text heraus.
    /// </remarks>
    [ModuleInitializer]
    internal static void Register()
        => LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<MaskingLayoutRenderer>("masked"));

    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        => builder.Append(Mask_(logEvent.FormattedMessage));

    /// <summary>Maskiert alle bekannten Secret-Formen. Intern für Tests sichtbar.</summary>
    internal static string Mask_(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var s = input;
        foreach (var p in Patterns)
        {
            // Gruppen-1-Muster (reine Key-Formate) werden komplett ersetzt,
            // Gruppen-2-Muster behalten den Präfix ("api_key=") und maskieren den Wert.
            s = p.Replace(s, m => m.Groups.Count > 2 && m.Groups[2].Success
                ? m.Groups[1].Value + Mask
                : Mask);
        }
        return s;
    }
}
