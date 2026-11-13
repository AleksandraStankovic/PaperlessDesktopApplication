using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

/// <summary>The side panel: selected document's metadata and file preview.</summary>
public partial class DocumentPreviewViewModel : ObservableObject
{
    // WebView2 must never get a null address, so "no preview" is an empty page
    private static readonly Uri BlankPage = new("about:blank");

    private readonly DocumentFileService _files;
    private readonly PaperlessApiClient _api;
    private readonly LookupCache _lookups;
    private readonly NotificationService _notifications;
    private readonly LocalizationService _localization;

    private CancellationTokenSource? _loadCts;
    private string? _previewFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    [NotifyCanExecuteChangedFor(nameof(OpenTagPickerCommand))]
    private DocumentItemViewModel? _document;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    private bool _isLoadingPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    private Uri _previewUri = BlankPage;

    [ObservableProperty]
    private bool _previewFailed;

    public bool IsOpen => Document is not null;

    public bool ShowPreview => !IsLoadingPreview && PreviewUri != BlankPage;

    /// <summary>Raised when the user closes the panel (the list then clears its selection).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Raised after a document's tags were saved. True if a new tag was created as well
    /// (the filter dropdown and the Tags page then need the new tag too).
    /// </summary>
    public event EventHandler<bool>? TagsEdited;

    // ---------- Tag editing ----------

    /// <summary>Tags that can still be added (not on the document yet), filtered by the search text.</summary>
    public ObservableCollection<Tag> TagSuggestions { get; } = new();

    /// <summary>The "+ Dodaj tag" popup.</summary>
    [ObservableProperty]
    private bool _isTagPickerOpen;

    /// <summary>What the user typed in the popup: filters the list, or becomes the name of a new tag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewTagName))]
    [NotifyPropertyChangedFor(nameof(CanCreateTag))]
    [NotifyPropertyChangedFor(nameof(ShowNoSuggestions))]
    [NotifyCanExecuteChangedFor(nameof(CreateTagCommand))]
    private string _tagSearchText = "";

    /// <summary>True while a change is being saved. The tag controls are disabled meanwhile,
    /// so two quick clicks can't overwrite each other.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditTags))]
    [NotifyCanExecuteChangedFor(nameof(RemoveTagCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddTagCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateTagCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenTagPickerCommand))]
    private bool _isSavingTags;

    public bool CanEditTags => !IsSavingTags;

    public string NewTagName => TagSearchText?.Trim() ?? "";

    /// <summary>
    /// "Kreiraj tag „…“" is offered when the typed name is valid and no tag has that name yet
    /// (an existing tag with the same name is simply shown in the list instead).
    /// </summary>
    public bool CanCreateTag =>
        NewTagName.Length is > 0 and <= TagEditorViewModel.MaxNameLength
        && !_lookups.Tags.Any(t => string.Equals(t.Name, NewTagName, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Nothing to pick and nothing to create (e.g. the document already has every tag).</summary>
    public bool ShowNoSuggestions => TagSuggestions.Count == 0 && !CanCreateTag;

    public DocumentPreviewViewModel(DocumentFileService files, PaperlessApiClient api, LookupCache lookups,
                                    NotificationService notifications, LocalizationService localization)
    {
        _files = files;
        _api = api;
        _lookups = lookups;
        _notifications = notifications;
        _localization = localization;
    }

    /// <summary>Shows a document in the panel, or closes the panel if item is null.</summary>
    public async Task ShowAsync(DocumentItemViewModel? item)
    {
        // Stop loading the previously selected document
        _loadCts?.Cancel();

        Document = item;
        IsTagPickerOpen = false;
        PreviewUri = BlankPage;
        PreviewFailed = false;
        SetPreviewFile(null);

        if (item is null)
        {
            IsLoadingPreview = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoadingPreview = true;
        try
        {
            string path = await _files.GetPreviewFileAsync(item.Model, cts.Token);
            cts.Token.ThrowIfCancellationRequested();   // user selected another document meanwhile

            SetPreviewFile(path);
            PreviewUri = new Uri(path);
        }
        catch (OperationCanceledException)
        {
            // Another document was selected - nothing to report
        }
        catch (Exception ex)
        {
            PreviewFailed = true;
            _notifications.ShowError(ex);
        }
        finally
        {
            if (_loadCts == cts)
            {
                _loadCts = null;
                IsLoadingPreview = false;
            }
            cts.Dispose();
        }
    }

    // ---------- Commands ----------

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanOpenExternally))]
    private void OpenExternally()
    {
        if (_previewFilePath is not null)
            OpenFile(_previewFilePath);
    }

    private bool CanOpenExternally() => _previewFilePath is not null;

    /// <summary>
    /// The ↗ button on a list row: opens that document in the default app without opening
    /// the preview panel. Uses the same downloaded file as the preview (cached, so a document
    /// that was already previewed opens immediately). While a file is downloading, the row
    /// buttons are disabled, so a double click doesn't open the document twice.
    /// </summary>
    [RelayCommand]
    private async Task OpenInDefaultAppAsync(DocumentItemViewModel? item)
    {
        if (item is null)
            return;

        try
        {
            string path = await _files.GetPreviewFileAsync(item.Model, CancellationToken.None);
            OpenFile(path);
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
        }
    }

    // ---------- Tag commands ----------

    [RelayCommand(CanExecute = nameof(CanOpenTagPicker))]
    private void OpenTagPicker()
    {
        TagSearchText = "";
        RefreshTagSuggestions();
        IsTagPickerOpen = true;
    }

    private bool CanOpenTagPicker() => Document is not null && !IsSavingTags;

    [RelayCommand]
    private void CloseTagPicker() => IsTagPickerOpen = false;

    /// <summary>Clicking a tag in the popup adds it to the document.</summary>
    [RelayCommand(CanExecute = nameof(CanEditTags))]
    private async Task AddTagAsync(Tag? tag)
    {
        if (tag is null || Document is not { } item || item.Model.TagIds.Contains(tag.Id))
            return;

        IsTagPickerOpen = false;
        await SaveTagsAsync(item, item.Model.TagIds.Append(tag.Id).ToList(), createdTag: false);
    }

    /// <summary>
    /// Enter in the popup's search box: adds the top matching tag (an exact name match wins),
    /// or creates a new tag if nothing matches, so the keyboard alone is enough.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmTagSearchAsync()
    {
        if (IsSavingTags || NewTagName.Length == 0)
            return;

        var match = TagSuggestions.FirstOrDefault(t =>
                        string.Equals(t.Name, NewTagName, StringComparison.CurrentCultureIgnoreCase))
                    ?? TagSuggestions.FirstOrDefault();

        if (match is not null)
            await AddTagAsync(match);
        else if (CanCreateTag)
            await CreateTagAsync();
    }

    /// <summary>"Kreiraj tag „…“": creates the tag on the server and adds it to the document.</summary>
    [RelayCommand(CanExecute = nameof(CanCreateNewTag))]
    private async Task CreateTagAsync()
    {
        if (Document is not { } item || !CanCreateTag)
            return;

        string name = NewTagName;
        IsTagPickerOpen = false;
        IsSavingTags = true;

        Tag created;
        try
        {
            created = await _api.CreateTagAsync(name, PickColorForNewTag());
        }
        catch (Exception ex)
        {
            IsSavingTags = false;
            _notifications.ShowError(ex);
            return;
        }

        _lookups.AddOrUpdateTag(created);
        bool saved = await SaveTagsAsync(item, item.Model.TagIds.Append(created.Id).ToList(), createdTag: true);

        if (saved)
            _notifications.ShowSuccess(string.Format(_localization.Get("Preview_TagCreated"), created.Name));
    }

    private bool CanCreateNewTag() => !IsSavingTags && CanCreateTag;

    /// <summary>The ✕ on a tag: removes it from the document right away, with "Poništi" to undo (H3).</summary>
    [RelayCommand(CanExecute = nameof(CanEditTags))]
    private async Task RemoveTagAsync(Tag? tag)
    {
        if (tag is null || Document is not { } item || !item.Model.TagIds.Contains(tag.Id))
            return;

        bool saved = await SaveTagsAsync(item, item.Model.TagIds.Where(id => id != tag.Id).ToList(), createdTag: false);
        if (!saved)
            return;

        _notifications.Show(
            string.Format(_localization.Get("Preview_TagRemoved"), tag.Name),
            NotificationKind.Success,
            _localization.Get("Common_Undo"),
            () => _ = UndoRemoveAsync(item, tag));
    }

    private async Task UndoRemoveAsync(DocumentItemViewModel item, Tag tag)
    {
        if (IsSavingTags || item.Model.TagIds.Contains(tag.Id) || _lookups.GetTag(tag.Id) is null)
            return;   // already back, or the tag was deleted meanwhile

        await SaveTagsAsync(item, item.Model.TagIds.Append(tag.Id).ToList(), createdTag: false);
    }

    /// <summary>
    /// Sends the document's new tag list to the server. The chips change only after the server
    /// accepted it, so what the user sees is always what is saved.
    /// </summary>
    private async Task<bool> SaveTagsAsync(DocumentItemViewModel item, List<int> newTagIds, bool createdTag)
    {
        IsSavingTags = true;
        try
        {
            var oldTagIds = item.Model.TagIds.ToList();
            var updated = await _api.UpdateDocumentTagsAsync(item.Id, newTagIds);

            foreach (int id in updated.TagIds.Except(oldTagIds))
                _lookups.AdjustTagCount(id, +1);
            foreach (int id in oldTagIds.Except(updated.TagIds))
                _lookups.AdjustTagCount(id, -1);

            item.ApplyTagUpdate(updated, _lookups);
            TagsEdited?.Invoke(this, createdTag);
            return true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
            if (createdTag)
                TagsEdited?.Invoke(this, true);   // the new tag exists even though adding it failed
            return false;
        }
        finally
        {
            IsSavingTags = false;
        }
    }

    partial void OnTagSearchTextChanged(string value) => RefreshTagSuggestions();

    private void RefreshTagSuggestions()
    {
        TagSuggestions.Clear();
        if (Document is { } item)
        {
            string search = NewTagName;
            var compare = CultureInfo.CurrentCulture.CompareInfo;
            const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;   // "cas" finds "čas"

            var matches = _lookups.Tags
                .Where(t => !item.Model.TagIds.Contains(t.Id))
                .Where(t => search.Length == 0 || compare.IndexOf(t.Name, search, options) >= 0)
                .OrderBy(t => compare.IsPrefix(t.Name, search, options) ? 0 : 1)   // "rač" → "računi" before "obračun"
                .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase);

            foreach (var tag in matches)
                TagSuggestions.Add(tag);
        }
        OnPropertyChanged(nameof(CanCreateTag));
        OnPropertyChanged(nameof(ShowNoSuggestions));
        CreateTagCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// A new tag from the preview gets the palette color used least so far, so tags stay easy to
    /// tell apart. The color can be changed later on the Tags page.
    /// </summary>
    private string PickColorForNewTag()
    {
        var palette = TagEditorViewModel.PresetColorValues;
        var used = _lookups.Tags
            .GroupBy(t => (t.Color ?? "").ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Count());
        return palette
            .Where(c => c != "#000000" && c != "#666666" && c != "#cccccc")   // greys look "disabled"
            .OrderBy(c => used.GetValueOrDefault(c, 0))
            .First();
    }

    private void OpenFile(string path)
    {
        if (!_files.TryOpenWithDefaultApp(path))
            _notifications.ShowLocalized("Preview_OpenFailed", NotificationKind.Error);
    }

    private void SetPreviewFile(string? path)
    {
        _previewFilePath = path;
        OpenExternallyCommand.NotifyCanExecuteChanged();   // enable/disable the ↗ button
    }
}