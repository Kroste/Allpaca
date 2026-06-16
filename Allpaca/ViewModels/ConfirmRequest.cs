namespace Allpaca.ViewModels;

/// <summary>
/// Parameter fuer einen Bestaetigungsdialog. Die View bindet direkt auf diese
/// Properties (DataContext = ConfirmRequest), ein eigenes ViewModel ist hier
/// uebertrieben.
/// </summary>
public sealed record ConfirmRequest(
    string Title,
    string Message,
    string ConfirmLabel,
    bool IsDestructive);
