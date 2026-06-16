namespace Allpaca.ViewModels;

/// <summary>
/// Zusatzinfos, die das LogWindow neben dem reinen Stream braucht: Titel der
/// Operation und ob am Ende ein Reboot-Hinweis angezeigt werden soll
/// (relevant fuer rpm-ostree, ggf. spaeter andere immutable-OS-Pfade).
/// </summary>
public sealed record OperationContext(
    string Title,
    bool RequiresReboot = false);
