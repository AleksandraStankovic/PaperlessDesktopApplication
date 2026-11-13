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

    /// <summary>The document's tags. Replaced (not changed in place) after they are edited in the preview.</summary>
    [ObservableProperty]
    private IReadOnlyList<Tag> _tags = Array.Empty<Tag>();

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
        RefreshTags(lookups);
    }

    /// <summary>Takes over the server's answer after the tags were changed in the preview panel.</summary>
    public void ApplyTagUpdate(Document updated, LookupCache lookups)
    {
        Model.TagIds = updated.TagIds;
        if (updated.Modified is not null)
        {
            Model.Modified = updated.Modified;
            OnPropertyChanged(nameof(Modified));
        }
        RefreshTags(lookups);
    }

    private void RefreshTags(LookupCache lookups)
    {
        Tags = Model.TagIds
            .Select(lookups.GetTag)
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();
    }
}