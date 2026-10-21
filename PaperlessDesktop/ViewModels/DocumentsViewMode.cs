using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class DocumentsViewModel : ObservableObject
{
    private readonly PaperlessApiClient _api;
    private readonly LookupCache _lookups;
    private readonly NotificationService _notifications;

    // The current request. Search (Phase 4) and filters (Phase 5) will also change this object.
    private readonly DocumentQuery _query = new();

    private CancellationTokenSource? _loadCts;
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
    private Task RefreshAsync() => LoadPageAsync(CurrentPage, reloadLookups: true);

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private Task NextPageAsync() => LoadPageAsync(CurrentPage + 1);

    private bool CanGoToNextPage() => CurrentPage < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private Task PreviousPageAsync() => LoadPageAsync(CurrentPage - 1);

    private bool CanGoToPreviousPage() => CurrentPage > 1;

    // Runs automatically when the user picks a different page size
    partial void OnPageSizeChanged(int value)
    {
        if (_initialized)
            _ = LoadPageAsync(1);
    }

    // ---------- Loading ----------

    private async Task LoadPageAsync(int page, bool reloadLookups = false)
    {
        // Cancel a previous load that is still running (e.g. user clicked "next" twice quickly)
        _loadCts?.Cancel();
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
                Documents.Add(new DocumentItemViewModel(doc, _lookups));

            TotalCount = result.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling(result.Count / (double)PageSize));
            CurrentPage = page;
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
}