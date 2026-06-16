namespace Allpaca.Models;

/// <summary>Eine Zeile Live-Ausgabe einer laufenden Operation (v2).</summary>
public sealed record ProgressLine(string Text, bool IsError);
