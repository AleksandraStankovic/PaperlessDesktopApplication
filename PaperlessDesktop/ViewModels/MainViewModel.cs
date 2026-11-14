using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ObservableObject[] _pages;
    private readonly DocumentsViewModel _documents;
    private readonly NotificationService _notifications;

    public SnackbarMessageQueue MessageQueue { get; }

    [ObservableProperty]
    private ObservableObject? _currentPage;

    [ObservableProperty]
    private int _selectedPageIndex;

    public MainViewModel(
        DocumentsViewModel documents,
        AnalyticsViewModel analytics,
        UploadViewModel upload,
        TagsViewModel tags,
        SettingsViewModel settings,
        NotificationService notifications)
    {
        _pages = new ObservableObject[] { documents, analytics, upload, tags, settings };
        _documents = documents;
        CurrentPage = documents;
        _notifications = notifications;
        MessageQueue = notifications.MessageQueue;

        _ = documents.InitializeAsync();
    }

    [RelayCommand]
    private void DismissNotification() => _notifications.DismissCurrent();

    public void ShowDocuments() => SelectedPageIndex = 0;

    public void ShowAnalytics() => SelectedPageIndex = 1;

    public void ShowUpload() => SelectedPageIndex = 2;

    partial void OnSelectedPageIndexChanged(int value)
    {
        if (value < 0 || value >= _pages.Length)
            return;

        CurrentPage = _pages[value];

        if (value != 0)
            _documents.CameFromAnalytics = false;

        if (CurrentPage is INavigationAware page)
            page.OnNavigatedTo();
    }
}