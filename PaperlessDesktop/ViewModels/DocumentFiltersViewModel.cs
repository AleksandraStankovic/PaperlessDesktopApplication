using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

/// <summary>
/// Filter options (dropdowns) and active filters (chips) for the document list.
/// Raises FiltersChanged whenever the user adds or removes a filter.
/// </summary>
public partial class DocumentFiltersViewModel : ObservableObject
{
    // Options for the three dropdowns
    public ObservableCollection<Tag> AvailableTags { get; } = new();
    public ObservableCollection<Correspondent> AvailableCorrespondents { get; } = new();
    public ObservableCollection<DocumentType> AvailableDocumentTypes { get; } = new();

    // Currently active filters, shown as chips
    public ObservableCollection<FilterChip> ActiveFilters { get; } = new();

    public bool HasActiveFilters => ActiveFilters.Count > 0;

    /// <summary>Raised when the user adds or removes a filter.</summary>
    public event EventHandler? FiltersChanged;

    // The dropdowns are "pickers": choosing an item adds a chip, then the dropdown resets
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

    /// <summary>
    /// Fills the dropdowns from the lookup cache (alphabetically, č/ć/š sorted correctly)
    /// and keeps active chips in sync after tags were renamed, recolored or deleted.
    /// </summary>
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
                ActiveFilters.RemoveAt(i);      // the item was deleted - drop its filter
            else
                ActiveFilters[i] = updated;     // refresh name / color
        }
    }

    /// <summary>Copies the active filters into the API query.</summary>
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

    /// <summary>Removes all filters without raising FiltersChanged (used by "reset everything").</summary>
    public void ClearSilently() => ActiveFilters.Clear();


    /// <summary>Replaces all filters with this one, without raising FiltersChanged (used by Analytics).</summary>
    public void SetOnlySilently(FilterChip chip)
    {
        ActiveFilters.Clear();
        ActiveFilters.Add(chip);
    }

    // ---------- Commands ----------

    /// <summary>The ✕ on a chip: remove just this one filter.</summary>
    [RelayCommand]
    private void RemoveFilter(FilterChip? chip)
    {
        if (chip is not null && ActiveFilters.Remove(chip))
            RaiseFiltersChanged();
    }

    /// <summary>"Clear all filters" button.</summary>
    [RelayCommand]
    private void ClearAll()
    {
        if (ActiveFilters.Count == 0)
            return;
        ActiveFilters.Clear();
        RaiseFiltersChanged();
    }

    // ---------- Reactions to the dropdowns ----------

    partial void OnTagToAddChanged(Tag? value)
    {
        if (value is null)
            return;

        // Tags: several allowed, but not the same tag twice
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

    // ---------- Helpers ----------

    /// <summary>Correspondent and type: only one at a time, a new choice replaces the old chip.</summary>
    private void SetSingleFilter(FilterKind kind, int id, string name)
    {
        var existing = ActiveFilters.FirstOrDefault(f => f.Kind == kind);
        if (existing?.Id == id)
            return;   // same choice again - nothing to do

        if (existing is not null)
            ActiveFilters.Remove(existing);

        ActiveFilters.Add(new FilterChip(kind, id, name));
        RaiseFiltersChanged();
    }

    private async Task ResetPickersAsync()
    {
        // Wait until the ComboBox has finished handling the click, then clear its selection
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