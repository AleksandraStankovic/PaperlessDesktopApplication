using System.Windows.Media;
using PaperlessDesktop.Models;

namespace PaperlessDesktop.ViewModels;

public class PieSliceViewModel
{
    public string Name { get; }
    public int Count { get; }
    public double Percent { get; }
    public string Color { get; }
    public Geometry Geometry { get; }

    public int? DocumentTypeId { get; }

    public bool IsClickable => DocumentTypeId is not null;
    public string PercentText => $"{Percent:0}%";
    public string Tooltip => $"{Name}: {Count} ({Percent:0}%)";

    public PieSliceViewModel(string name, int count, double percent, string color, Geometry geometry, int? documentTypeId)
    {
        Name = name;
        Count = count;
        Percent = percent;
        Color = color;
        Geometry = geometry;
        DocumentTypeId = documentTypeId;
    }
}

public class MonthBarViewModel
{
    public int Year { get; }
    public int Month { get; }
    public string Label { get; }
    public string FullName { get; }
    public int Count { get; }
    public double BarHeight { get; }

    public bool HasDocuments => Count > 0;
    public string Tooltip => $"{FullName} {Year}: {Count}";

    public MonthBarViewModel(int year, int month, string label, string fullName, int count, double barHeight)
    {
        Year = year;
        Month = month;
        Label = label;
        FullName = fullName;
        Count = count;
        BarHeight = barHeight;
    }
}

public class TagCloudItemViewModel
{
    public Tag Tag { get; }
    public double FontSize { get; }
    public string Tooltip => $"{Tag.Name}: {Tag.DocumentCount}";

    public TagCloudItemViewModel(Tag tag, double fontSize)
    {
        Tag = tag;
        FontSize = fontSize;
    }
}