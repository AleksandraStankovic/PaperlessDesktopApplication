using System.Windows.Media;
using PaperlessDesktop.Models;

namespace PaperlessDesktop.ViewModels;

/// <summary>One slice of the "by document type" donut chart.</summary>
public class PieSliceViewModel
{
    public string Name { get; }
    public int Count { get; }
    public double Percent { get; }
    public string Color { get; }
    public Geometry Geometry { get; }

    /// <summary>null for "Other" / "No type" - those slices can't be clicked.</summary>
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

/// <summary>One bar of the "by month" chart.</summary>
public class MonthBarViewModel
{
    public int Year { get; }
    public int Month { get; }
    public string Label { get; }      // short name, e.g. "mar"
    public string FullName { get; }   // e.g. "Mart"
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

/// <summary>One word in the tag cloud (bigger font = used by more documents).</summary>
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