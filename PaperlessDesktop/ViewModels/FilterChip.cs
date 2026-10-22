namespace PaperlessDesktop.ViewModels;

public enum FilterKind
{
    Tag,
    Correspondent,
    DocumentType
}

public class FilterChip
{
    public FilterKind Kind { get; }
    public int Id { get; }
    public string Name { get; }

    public FilterChip(FilterKind kind, int id, string name)
    {
        Kind = kind;
        Id = id;
        Name = name;
    }
}