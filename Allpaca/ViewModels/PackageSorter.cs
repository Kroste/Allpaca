namespace Allpaca.ViewModels;

internal static class PackageSorter
{
    public static IEnumerable<PackageItemViewModel> Sort(
        IEnumerable<PackageItemViewModel> input, SortKey key, bool descending) => key switch
    {
        SortKey.Size => SortBySize(input, descending),
        SortKey.Source => SortBySource(input, descending),
        _ => SortByName(input, descending),
    };

    private static IEnumerable<PackageItemViewModel> SortByName(
        IEnumerable<PackageItemViewModel> input, bool descending) =>
        descending
            ? input.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase)
            : input.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<PackageItemViewModel> SortBySize(
        IEnumerable<PackageItemViewModel> input, bool descending)
    {
        // Einträge ohne bekannte Größe landen immer am Ende - unabhängig von asc/desc.
        var primary = input.OrderBy(p => p.Model.SizeBytes is null ? 1 : 0);
        var sized = descending
            ? primary.ThenByDescending(p => p.Model.SizeBytes ?? 0L)
            : primary.ThenBy(p => p.Model.SizeBytes ?? 0L);
        return sized.ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<PackageItemViewModel> SortBySource(
        IEnumerable<PackageItemViewModel> input, bool descending)
    {
        var primary = descending
            ? input.OrderByDescending(p => p.SourceLabel, StringComparer.OrdinalIgnoreCase)
            : input.OrderBy(p => p.SourceLabel, StringComparer.OrdinalIgnoreCase);
        return primary.ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }
}
