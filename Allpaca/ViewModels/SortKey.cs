namespace Allpaca.ViewModels;

public enum SortKey
{
    Name,
    Size,
    Source,
}

public sealed record SortOption(SortKey Key, string Label);
