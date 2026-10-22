using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

/// <summary>One row/card in the document list: a Document with names resolved.</summary>
public partial class DocumentItemViewModel : ObservableObject
{
    public Document Model { get; }

    public int Id => Model.Id;
    public string Title => Model.Title;
    public DateTime? Created => Model.Created;
    public DateTime? Added => Model.Added;
    public DateTime? Modified => Model.Modified;
    public string? OriginalFileName => Model.OriginalFileName;
    public int? PageCount => Model.PageCount;
    public int? ArchiveSerialNumber => Model.ArchiveSerialNumber;

    public string CorrespondentName { get; }
    public string DocumentTypeName { get; }
    public IReadOnlyList<Tag> Tags { get; }

    // Thumbnail arrives later (downloaded in the background)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private ImageSource? _thumbnail;

    public bool HasThumbnail => Thumbnail is not null;

    /// <summary>True once a download has been started, so we don't request it twice.</summary>
    public bool ThumbnailRequested { get; set; }

    public DocumentItemViewModel(Document model, LookupCache lookups)
    {
        Model = model;
        CorrespondentName = lookups.GetCorrespondentName(model.CorrespondentId) ?? "—";
        DocumentTypeName = lookups.GetDocumentTypeName(model.DocumentTypeId) ?? "—";
        Tags = model.TagIds
            .Select(lookups.GetTag)
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();
    }
}