namespace Allpaca.ViewModels;

/// <summary>
/// Parameter für einen Bestätigungsdialog. Die View bindet direkt auf diese
/// Properties (DataContext = ConfirmRequest), ein eigenes ViewModel ist hier
/// übertrieben.
/// </summary>
public sealed record ConfirmRequest(
    string Title,
    string Message,
    string ConfirmLabel,
    bool IsDestructive);
