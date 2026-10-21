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

    public string CorrespondentName { get; }
    public string DocumentTypeName { get; }
    public IReadOnlyList<Tag> Tags { get; }

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