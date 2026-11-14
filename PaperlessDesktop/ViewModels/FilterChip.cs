using PaperlessDesktop.Models;

namespace PaperlessDesktop.ViewModels;

public enum FilterKind
{
    Tag,
    Correspondent,
    DocumentType,
    Month
}

public class FilterChip
{
    public const int NoDocumentTypeId = 0;

    public FilterKind Kind { get; }
    public int Id { get; }
    public string Name { get; }

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

    public static FilterChip ForMonth(int year, int month, string displayName)
        => new(FilterKind.Month, year * 100 + month, displayName);
}