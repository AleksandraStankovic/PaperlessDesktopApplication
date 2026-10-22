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

    private readonly PaperlessApiClient _api;
    private readonly LookupCache _lookups;
    private readonly NotificationService _notifications;

    // The current request. Search (Phase 4) and filters (Phase 5) will also change this object.
    private readonly DocumentQuery _query = new();

    // Thumbnails already downloaded (by document ID), so going back a page is instant
    private readonly Dictionary<int, ImageSource> _thumbnailCache = new();

    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _thumbnailCts;
    private bool _initialized;

    public ObservableCollection<DocumentItemViewModel> Documents { get; } = new();

    public int[] PageSizeOptions { get; } = { 10, 25, 50, 100 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoDocuments))]
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

    /// <summary>True when loading is finished and there is nothing to show.</summary>
    public bool HasNoDocuments => !IsLoading && Documents.Count == 0;

    public DocumentsViewModel(PaperlessApiClient api, LookupCache lookups, NotificationService notifications)
    {
        _api = api;
        _lookups = lookups;
        _notifications = notifications;
    }

    /// <summary>Called once when the app starts.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;
        await LoadPageAsync(1);
    }

    // ---------- Commands (bound to buttons) ----------

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

    // ---------- Reactions to property changes ----------

    partial void OnPageSizeChanged(int value)
    {
        if (_initialized)
            _ = LoadPageAsync(1);
    }

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
                await _lookups.LoadAsync(cts.Token);

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