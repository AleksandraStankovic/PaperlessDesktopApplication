using PaperlessDesktop.Models;

namespace PaperlessDesktop.ViewModels;

public enum FilterKind
{
    Tag,
    Correspondent,
    DocumentType,
    Month
}

/// <summary>One active filter, shown as a removable chip.</summary>
public class FilterChip
{
    public FilterKind Kind { get; }
    public int Id { get; }
    public string Name { get; }

    // Only set for tag filters: the chip is shown in the tag's own color
    public string? Color { get; }
    public string? TextColor { get; }

    public FilterChip(FilterKind kind, int id, string name, string? color = null, string? textColor = null)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Color = color;
        TextColor = textColor;
    }

    public static FilterChip ForTag(Tag tag) => new(FilterKind.Tag, tag.Id, tag.Name, tag.Color, tag.TextColor);


    /// <summary>A "documents from this month" filter. The ID stores both values: 2025 * 100 + 3 = 202503.</summary>
    public static FilterChip ForMonth(int year, int month, string displayName)
        => new(FilterKind.Month, year * 100 + month, displayName);
}