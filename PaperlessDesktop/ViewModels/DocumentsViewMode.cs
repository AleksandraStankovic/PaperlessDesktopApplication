using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class DocumentsViewModel : ObservableObject
{
    private const int ParallelThumbnailDownloads = 4;
    private const int SearchDelayMilliseconds = 300;

    private readonly PaperlessApiClient _api;
    private readonly LookupCache _lookups;
    private readonly NotificationService _notifications;
    private readonly DialogService _dialogs;
    private readonly LocalizationService _localization;

    private readonly DocumentQuery _query = new();

    private readonly Dictionary<int, ImageSource> _thumbnailCache = new();

    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _thumbnailCts;
    private CancellationTokenSource? _searchDelayCts;
    private bool _initialized;

    public ObservableCollection<DocumentItemViewModel> Documents { get; } = new();

    public int[] PageSizeOptions { get; } = { 10, 25, 50, 100 };

    public DocumentFiltersViewModel Filters { get; } = new();

    public DocumentPreviewViewModel Preview { get; }

    [ObservableProperty]
    private DocumentItemViewModel? _selectedDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoDocuments))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowNoResults))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = 25;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTableView))]
    [NotifyPropertyChangedFor(nameof(IsCardView))]
    private int _viewModeIndex;

    public bool IsTableView => ViewModeIndex != 1;
    public bool IsCardView => ViewModeIndex == 1;

    [ObservableProperty]
    private string _searchText = "";

    public string ActiveSearchText => _query.SearchText ?? "";

    public bool IsSearchActive => !string.IsNullOrEmpty(_query.SearchText);

    public bool IsQueryActive => IsSearchActive || Filters.HasActiveFilters;

    public bool HasNoDocuments => !IsLoading && Documents.Count == 0;

    public bool ShowEmptyState => HasNoDocuments && !IsQueryActive;

    public bool ShowNoResults => HasNoDocuments && IsQueryActive;

    public event EventHandler<bool>? DocumentTagsEdited;

    public event EventHandler? DocumentDeleted;

    public DocumentsViewModel(PaperlessApiClient api, LookupCache lookups, DocumentFileService files,
                              NotificationService notifications, DialogService dialogs, LocalizationService localization)
    {
        _api = api;
        _lookups = lookups;
        _notifications = notifications;
        _dialogs = dialogs;
        _localization = localization;

        Preview = new DocumentPreviewViewModel(files, api, lookups, notifications, localization);
        Preview.CloseRequested += (_, _) => SelectedDocument = null;
        Preview.TagsEdited += (_, createdTag) =>
        {
            Filters.UpdateOptions(_lookups);
            DocumentTagsEdited?.Invoke(this, createdTag);
        };

        Filters.FiltersChanged += (_, _) => OnFiltersChanged();
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await LoadPageAsync(1);
    }

    public Task ReloadLookupsAsync()
        => _initialized ? LoadPageAsync(CurrentPage, reloadLookups: true) : Task.CompletedTask;

    [ObservableProperty]
    private bool _cameFromAnalytics;

    public event EventHandler? BackToAnalyticsRequested;

    [RelayCommand]
    private void BackToAnalytics() => BackToAnalyticsRequested?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    private bool _cameFromTags;

    public event EventHandler? BackToTagsRequested;

    [RelayCommand]
    private void BackToTags() => BackToTagsRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler? AddDocumentRequested;

    [RelayCommand]
    private void AddDocument() => AddDocumentRequested?.Invoke(this, EventArgs.Empty);

    public void ShowWithFilter(FilterChip chip, bool fromTags = false)
    {
        CameFromAnalytics = !fromTags;
        CameFromTags = fromTags;
        Filters.SetOnlySilently(chip);
        SearchText = "";
        _searchDelayCts?.Cancel();

        _query.SearchText = null;
        Filters.ApplyTo(_query);
        NotifyQueryStateChanged();
        _ = LoadPageAsync(1);
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        _thumbnailCache.Clear();
        return LoadPageAsync(CurrentPage, reloadLookups: true);
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync(DocumentItemViewModel? item)
    {
        if (item is null)
            return;

        bool confirmed = await _dialogs.ConfirmAsync(
            _localization.Get("Documents_DeleteTitle"),
            string.Format(_localization.Get("Documents_DeleteConfirm"), item.Title),
            _localization.Get("Common_Delete"), isDestructive: true);
        if (!confirmed)
            return;

        try
        {
            IsLoading = true;
            await _api.DeleteDocumentAsync(item.Id);
        }
        catch (Exception ex)
        {
            IsLoading = false;
            _notifications.ShowError(ex);
            return;
        }

        if (ReferenceEquals(SelectedDocument, item) || SelectedDocument?.Id == item.Id)
            SelectedDocument = null;
        _thumbnailCache.Remove(item.Id);

        _notifications.ShowSuccessLonger(string.Format(_localization.Get("Documents_Deleted"), item.Title));

        int page = Documents.Count <= 1 && CurrentPage > 1 ? CurrentPage - 1 : CurrentPage;

        await LoadPageAsync(page, reloadLookups: true);
        DocumentDeleted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private Task NextPageAsync() => LoadPageAsync(CurrentPage + 1);

    private bool CanGoToNextPage() => CurrentPage < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private Task PreviousPageAsync() => LoadPageAsync(CurrentPage - 1);

    private bool CanGoToPreviousPage() => CurrentPage > 1;

    [RelayCommand]
    private Task SearchNowAsync()
    {
        _searchDelayCts?.Cancel();
        return ApplySearchAsync();
    }

    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = "";
        _searchDelayCts?.Cancel();
        return ApplySearchAsync();
    }

    [RelayCommand]
    private Task ResetAllAsync()
    {
        Filters.ClearSilently();
        SearchText = "";
        _searchDelayCts?.Cancel();

        _query.SearchText = null;
        Filters.ApplyTo(_query);
        NotifyQueryStateChanged();
        return LoadPageAsync(1);
    }

    partial void OnPageSizeChanged(int value)
    {
        if (_initialized)
            _ = LoadPageAsync(1);
    }

    partial void OnSelectedDocumentChanged(DocumentItemViewModel? value)
        => _ = Preview.ShowAsync(value);

    partial void OnViewModeIndexChanged(int oldValue, int newValue)
    {
        if (newValue < 0)
        {
            ViewModeIndex = oldValue;
            return;
        }

        if (IsCardView)
            StartThumbnailLoading();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDelayCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDelayCts = cts;
        _ = SearchAfterDelayAsync(cts.Token);
    }

    private async Task SearchAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchDelayMilliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await ApplySearchAsync();
    }

    private Task ApplySearchAsync()
    {
        string text = SearchText?.Trim() ?? "";

        if (text == ActiveSearchText)
            return Task.CompletedTask;

        _query.SearchText = text.Length == 0 ? null : text;
        NotifyQueryStateChanged();

        return LoadPageAsync(1);
    }

    private void OnFiltersChanged()
    {
        Filters.ApplyTo(_query);
        NotifyQueryStateChanged();
        _ = LoadPageAsync(1);
    }

    private void NotifyQueryStateChanged()
    {
        OnPropertyChanged(nameof(ActiveSearchText));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(IsQueryActive));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private async Task LoadPageAsync(int page, bool reloadLookups = false)
    {
        _loadCts?.Cancel();
        _thumbnailCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        IsLoading = true;
        try
        {
            if (!_lookups.IsLoaded || reloadLookups)
            {
                await _lookups.LoadAsync(cts.Token);
                Filters.UpdateOptions(_lookups);   
                Filters.ApplyTo(_query); 
            }

            _query.Page = page;
            _query.PageSize = PageSize;
            var result = await _api.GetDocumentsAsync(_query, cts.Token);

            Documents.Clear();
            foreach (var doc in result.Results)
            {
                var item = new DocumentItemViewModel(doc, _lookups);
                if (_thumbnailCache.TryGetValue(doc.Id, out var cached))
                    item.Thumbnail = cached;
                Documents.Add(item);
            }

            TotalCount = result.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling(result.Count / (double)PageSize));
            CurrentPage = page;

            if (IsCardView)
                StartThumbnailLoading();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
        }
        finally
        {
            if (_loadCts == cts)
            {
                _loadCts = null;
                IsLoading = false;
            }
            cts.Dispose();
        }
    }

    private void StartThumbnailLoading()
    {
        _thumbnailCts?.Cancel();
        var cts = new CancellationTokenSource();
        _thumbnailCts = cts;

        var pending = Documents.Where(d => !d.HasThumbnail && !d.ThumbnailRequested).ToList();
        foreach (var item in pending)
            item.ThumbnailRequested = true;

        _ = LoadThumbnailsAsync(pending, cts.Token);
    }

    private async Task LoadThumbnailsAsync(IReadOnlyList<DocumentItemViewModel> items, CancellationToken ct)
    {
        try
        {
            foreach (var batch in items.Chunk(ParallelThumbnailDownloads))
            {
                ct.ThrowIfCancellationRequested();
                await Task.WhenAll(batch.Select(item => LoadThumbnailAsync(item, ct)));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadThumbnailAsync(DocumentItemViewModel item, CancellationToken ct)
    {
        try
        {
            byte[] data = await _api.GetThumbnailAsync(item.Id, ct);

            var image = await Task.Run(() => ThumbnailDecoder.Decode(data), ct);
            if (image is null)
                return;

            item.Thumbnail = image;
            _thumbnailCache[item.Id] = image;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
        }
    }
}