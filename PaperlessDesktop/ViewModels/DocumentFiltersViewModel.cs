using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class DocumentFiltersViewModel : ObservableObject
{
    public ObservableCollection<Tag> AvailableTags { get; } = new();
    public ObservableCollection<Correspondent> AvailableCorrespondents { get; } = new();
    public ObservableCollection<DocumentType> AvailableDocumentTypes { get; } = new();

    public ObservableCollection<FilterChip> ActiveFilters { get; } = new();

    public bool HasActiveFilters => ActiveFilters.Count > 0;

    public event EventHandler? FiltersChanged;

    [ObservableProperty]
    private Tag? _tagToAdd;

    [ObservableProperty]
    private Correspondent? _correspondentToSet;

    [ObservableProperty]
    private DocumentType? _documentTypeToSet;

    public DocumentFiltersViewModel()
    {
        ActiveFilters.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasActiveFilters));
    }

    public void UpdateOptions(LookupCache lookups)
    {
        var comparer = StringComparer.CurrentCultureIgnoreCase;
        Replace(AvailableTags, lookups.Tags.OrderBy(t => t.Name, comparer));
        Replace(AvailableCorrespondents, lookups.Correspondents.OrderBy(c => c.Name, comparer));
        Replace(AvailableDocumentTypes, lookups.DocumentTypes.OrderBy(t => t.Name, comparer));

        for (int i = ActiveFilters.Count - 1; i >= 0; i--)
        {
            var chip = ActiveFilters[i];
            FilterChip? updated = chip.Kind switch
            {
                FilterKind.Tag => lookups.GetTag(chip.Id) is { } tag
                    ? FilterChip.ForTag(tag) : null,
                FilterKind.Correspondent => lookups.GetCorrespondentName(chip.Id) is { } correspondentName
                    ? new FilterChip(chip.Kind, chip.Id, correspondentName) : null,
                FilterKind.DocumentType when chip.Id == FilterChip.NoDocumentTypeId => chip,
                FilterKind.DocumentType => lookups.GetDocumentTypeName(chip.Id) is { } typeName
                    ? new FilterChip(chip.Kind, chip.Id, typeName) : null,
                FilterKind.Month => chip,
                _ => null
            };

            if (updated is null)
                ActiveFilters.RemoveAt(i);
            else
                ActiveFilters[i] = updated;
        }
    }

    public void ApplyTo(DocumentQuery query)
    {
        query.TagIds = ActiveFilters
            .Where(f => f.Kind == FilterKind.Tag)
            .Select(f => f.Id)
            .ToList();
        query.CorrespondentId = ActiveFilters.FirstOrDefault(f => f.Kind == FilterKind.Correspondent)?.Id;
        query.DocumentTypeId = ActiveFilters.FirstOrDefault(f => f.Kind == FilterKind.DocumentType)?.Id;

        var month = ActiveFilters.FirstOrDefault(f => f.Kind == FilterKind.Month);
        query.CreatedYear = month is null ? null : month.Id / 100;
        query.CreatedMonth = month is null ? null : month.Id % 100;
    }

    public void ClearSilently() => ActiveFilters.Clear();

    public void SetOnlySilently(FilterChip chip)
    {
        ActiveFilters.Clear();
        ActiveFilters.Add(chip);
    }

    [RelayCommand]
    private void RemoveFilter(FilterChip? chip)
    {
        if (chip is not null && ActiveFilters.Remove(chip))
            RaiseFiltersChanged();
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (ActiveFilters.Count == 0)
            return;
        ActiveFilters.Clear();
        RaiseFiltersChanged();
    }

    partial void OnTagToAddChanged(Tag? value)
    {
        if (value is null)
            return;

        bool alreadyActive = ActiveFilters.Any(f => f.Kind == FilterKind.Tag && f.Id == value.Id);
        if (!alreadyActive)
        {
            ActiveFilters.Add(FilterChip.ForTag(value));
            RaiseFiltersChanged();
        }
        _ = ResetPickersAsync();
    }

    partial void OnCorrespondentToSetChanged(Correspondent? value)
    {
        if (value is null)
            return;
        SetSingleFilter(FilterKind.Correspondent, value.Id, value.Name);
        _ = ResetPickersAsync();
    }

    partial void OnDocumentTypeToSetChanged(DocumentType? value)
    {
        if (value is null)
            return;
        SetSingleFilter(FilterKind.DocumentType, value.Id, value.Name);
        _ = ResetPickersAsync();
    }

    private void SetSingleFilter(FilterKind kind, int id, string name)
    {
        var existing = ActiveFilters.FirstOrDefault(f => f.Kind == kind);
        if (existing?.Id == id)
            return;

        if (existing is not null)
            ActiveFilters.Remove(existing);

        ActiveFilters.Add(new FilterChip(kind, id, name));
        RaiseFiltersChanged();
    }

    private async Task ResetPickersAsync()
    {
        await Task.Yield();
        TagToAdd = null;
        CorrespondentToSet = null;
        DocumentTypeToSet = null;
    }

    private void RaiseFiltersChanged() => FiltersChanged?.Invoke(this, EventArgs.Empty);

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}