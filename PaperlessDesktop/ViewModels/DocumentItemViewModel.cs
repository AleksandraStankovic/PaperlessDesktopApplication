using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

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

    [ObservableProperty]
    private IReadOnlyList<Tag> _tags = Array.Empty<Tag>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private ImageSource? _thumbnail;

    public bool HasThumbnail => Thumbnail is not null;

    public bool ThumbnailRequested { get; set; }

    public DocumentItemViewModel(Document model, LookupCache lookups)
    {
        Model = model;
        CorrespondentName = lookups.GetCorrespondentName(model.CorrespondentId) ?? "—";
        DocumentTypeName = lookups.GetDocumentTypeName(model.DocumentTypeId) ?? "—";
        RefreshTags(lookups);
    }

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