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

    // The current request: page, search text and filters
    private readonly DocumentQuery _query = new();

    // Thumbnails already downloaded (by document ID), so going back a page is instant
    private readonly Dictionary<int, ImageSource> _thumbnailCache = new();

    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _thumbnailCts;
    private CancellationTokenSource? _searchDelayCts;
    private bool _initialized;

    public ObservableCollection<DocumentItemViewModel> Documents { get; } = new();

    public int[] PageSizeOptions { get; } = { 10, 25, 50, 100 };

    /// <summary>Filter dropdowns and active filter chips.</summary>
    public DocumentFiltersViewModel Filters { get; } = new();

    /// <summary>Side panel with the selected document's preview and metadata.</summary>
    public DocumentPreviewViewModel Preview { get; }

    /// <summary>Selected row/card (shared by table and card view).</summary>
    [ObservableProperty]
    private DocumentItemViewModel? _selectedDocument;

    // ---------- State ----------

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

    // 0 = table, 1 = cards (bound to the toggle in the header)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTableView))]
    [NotifyPropertyChangedFor(nameof(IsCardView))]
    private int _viewModeIndex;

    public bool IsTableView => ViewModeIndex != 1;
    public bool IsCardView => ViewModeIndex == 1;

    // ---------- Search ----------

    /// <summary>What the user is typing right now (bound to the search box).</summary>
    [ObservableProperty]
    private string _searchText = "";

    /// <summary>The search that the currently shown results belong to.</summary>
    public string ActiveSearchText => _query.SearchText ?? "";

    public bool IsSearchActive => !string.IsNullOrEmpty(_query.SearchText);

    /// <summary>True if a search or any filter is narrowing the results.</summary>
    public bool IsQueryActive => IsSearchActive || Filters.HasActiveFilters;

    // ---------- Empty states ----------

    public bool HasNoDocuments => !IsLoading && Documents.Count == 0;

    /// <summary>No documents at all (no search or filter active).</summary>
    public bool ShowEmptyState => HasNoDocuments && !IsQueryActive;

    /// <summary>Search/filters are active but nothing matches.</summary>
    public bool ShowNoResults => HasNoDocuments && IsQueryActive;

    /// <summary>
    /// Raised after tags were added to or removed from a document in the preview
    /// (true if a new tag was created too). App.xaml.cs lets Analytics and the Tags page know.
    /// </summary>
    public event EventHandler<bool>? DocumentTagsEdited;

    public DocumentsViewModel(PaperlessApiClient api, LookupCache lookups, DocumentFileService files,
                              NotificationService notifications, LocalizationService localization)
    {
        _api = api;
        _lookups = lookups;
        _notifications = notifications;

        Preview = new DocumentPreviewViewModel(files, api, lookups, notifications, localization);
        Preview.CloseRequested += (_, _) => SelectedDocument = null;
        Preview.TagsEdited += (_, createdTag) =>
        {
            // New tag in the filter dropdown, updated counts ("računi (13)"). The list itself is not
            // reloaded, so the document stays where it is and the preview stays open.
            Filters.UpdateOptions(_lookups);
            DocumentTagsEdited?.Invoke(this, createdTag);
        };

        Filters.FiltersChanged += (_, _) => OnFiltersChanged();
    }

    /// <summary>Called once when the app starts.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await LoadPageAsync(1);
    }

    /// <summary>Called after tags were created/edited/deleted on the Tags page.</summary>
    public Task ReloadLookupsAsync()
        => _initialized ? LoadPageAsync(CurrentPage, reloadLookups: true) : Task.CompletedTask;

    // ---------- Commands (bound to buttons / keys) ----------


    // ---------- Coming from Analytics ----------

    /// <summary>True while the list was opened by clicking a chart on the Analytics page (shows the back button).</summary>
    [ObservableProperty]
    private bool _cameFromAnalytics;

    /// <summary>The back button was clicked. App.xaml.cs switches to the Analytics page.</summary>
    public event EventHandler? BackToAnalyticsRequested;

    [RelayCommand]
    private void BackToAnalytics() => BackToAnalyticsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>"Dodaj dokument" was clicked (header or empty state). App.xaml.cs opens the upload page,
    /// the same page as the sidebar item.</summary>
    public event EventHandler? AddDocumentRequested;

    [RelayCommand]
    private void AddDocument() => AddDocumentRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Called from Analytics: show exactly the documents behind a clicked chart element.</summary>
    public void ShowWithFilter(FilterChip chip)
    {
        CameFromAnalytics = true;
        Filters.SetOnlySilently(chip);
        SearchText = "";
        _searchDelayCts?.Cancel();   // setting SearchText started a timer - stop it

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

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private Task NextPageAsync() => LoadPageAsync(CurrentPage + 1);

    private bool CanGoToNextPage() => CurrentPage < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private Task PreviousPageAsync() => LoadPageAsync(CurrentPage - 1);

    private bool CanGoToPreviousPage() => CurrentPage > 1;

    /// <summary>Enter key: search immediately, without waiting for the delay.</summary>
    [RelayCommand]
    private Task SearchNowAsync()
    {
        _searchDelayCts?.Cancel();
        return ApplySearchAsync();
    }

    /// <summary>Esc key in the search box.</summary>
    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = "";
        _searchDelayCts?.Cancel();
        return ApplySearchAsync();
    }

    /// <summary>"Reset search and filters" button in the no-results panel.</summary>
    [RelayCommand]
    private Task ResetAllAsync()
    {
        Filters.ClearSilently();
        SearchText = "";
        _searchDelayCts?.Cancel();   // setting SearchText started a timer - stop it

        _query.SearchText = null;
        Filters.ApplyTo(_query);
        NotifyQueryStateChanged();
        return LoadPageAsync(1);     // one reload for both changes
    }

    // ---------- Reactions to property changes ----------

    partial void OnPageSizeChanged(int value)
    {
        if (_initialized)
            _ = LoadPageAsync(1);
    }

    // Clicking a row/card (or moving with arrow keys) opens it in the preview panel
    partial void OnSelectedDocumentChanged(DocumentItemViewModel? value)
        => _ = Preview.ShowAsync(value);


    partial void OnViewModeIndexChanged(int oldValue, int newValue)
    {
        // Clicking the already-selected toggle button would deselect it (-1). Keep the old mode instead.
        if (newValue < 0)
        {
            ViewModeIndex = oldValue;
            return;
        }

        if (IsCardView)
            StartThumbnailLoading();
    }

    // Runs on every keystroke in the search box
    partial void OnSearchTextChanged(string value)
    {
        // Restart the timer: only search when the user stops typing
        _searchDelayCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDelayCts = cts;
        _ = SearchAfterDelayAsync(cts.Token);
    }

    // ---------- Search & filters ----------

    private async Task SearchAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchDelayMilliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            return;   // user typed another character - a newer timer is running
        }

        await ApplySearchAsync();
    }

    private Task ApplySearchAsync()
    {
        // The ✕ button in the search box sets the text to null, not "" - treat both as empty
        string text = SearchText?.Trim() ?? "";

        // Nothing changed (e.g. user typed "ab" and deleted it again) - no request needed
        if (text == ActiveSearchText)
            return Task.CompletedTask;

        _query.SearchText = text.Length == 0 ? null : text;
        NotifyQueryStateChanged();

        // New search always starts at page 1
        return LoadPageAsync(1);
    }

    private void OnFiltersChanged()
    {
        Filters.ApplyTo(_query);
        NotifyQueryStateChanged();
        _ = LoadPageAsync(1);   // new filters always start at page 1
    }

    private void NotifyQueryStateChanged()
    {
        OnPropertyChanged(nameof(ActiveSearchText));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(IsQueryActive));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    // ---------- Loading the list ----------

    private async Task LoadPageAsync(int page, bool reloadLookups = false)
    {
        // Cancel a previous load that is still running (e.g. user clicked "next" twice quickly)
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

            // Thumbnails are only needed in card view
            if (IsCardView)
                StartThumbnailLoading();
        }
        catch (OperationCanceledException)
        {
            // A newer load replaced this one - nothing to report
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
        }
        finally
        {
            // Only the most recent load is allowed to turn the spinner off
            if (_loadCts == cts)
            {
                _loadCts = null;
                IsLoading = false;
            }
            cts.Dispose();
        }
    }

    // ---------- Loading thumbnails ----------

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
            // Download 4 at a time: fast, but kind to the shared server
            foreach (var batch in items.Chunk(ParallelThumbnailDownloads))
            {
                ct.ThrowIfCancellationRequested();
                await Task.WhenAll(batch.Select(item => LoadThumbnailAsync(item, ct)));
            }
        }
        catch (OperationCanceledException)
        {
            // User changed page - stop quietly
        }
    }

    private async Task LoadThumbnailAsync(DocumentItemViewModel item, CancellationToken ct)
    {
        try
        {
            byte[] data = await _api.GetThumbnailAsync(item.Id, ct);

            // Decode on a background thread so the UI doesn't stutter
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
            // One broken thumbnail is not worth an error message - the card keeps its placeholder icon
        }
    }
}