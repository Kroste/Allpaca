namespace Allpaca.Models;

/// <summary>
/// Eine Zeile Live-Ausgabe einer laufenden Operation (v2). Wenn <see cref="ExitCode"/>
/// gesetzt ist, handelt es sich um die abschließende Marker-Zeile vom ProcessRunner -
/// die enthält nur den Exit-Code und KEINEN sichtbaren Text. Der LogWindow filtert
/// Marker-Zeilen aus der Anzeige raus und kippt den OperationState entsprechend.
/// </summary>
public sealed record ProgressLine(string Text, bool IsError, int? ExitCode = null);
