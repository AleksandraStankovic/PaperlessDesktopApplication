using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ObservableObject[] _pages;
    private readonly DocumentsViewModel _documents;

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
        // Order must match the order of items in the sidebar
        _pages = new ObservableObject[] { documents, analytics, upload, tags, settings };
        _documents = documents;
        CurrentPage = documents;
        MessageQueue = notifications.MessageQueue;

        // Start loading documents right away, so the list is ready when the app opens
        _ = documents.InitializeAsync();
    }


    /// <summary>Switches to the Documents page (e.g. after clicking a chart).</summary>
    public void ShowDocuments() => SelectedPageIndex = 0;

    /// <summary>Switches to the Analytics page (back button on a list opened from a chart).</summary>
    public void ShowAnalytics() => SelectedPageIndex = 1;

    /// <summary>Switches to the Upload page ("Dodaj dokument" button on the Documents page).</summary>
    public void ShowUpload() => SelectedPageIndex = 2;

    // Generated hook: runs automatically whenever SelectedPageIndex changes
    partial void OnSelectedPageIndexChanged(int value)
    {
        if (value < 0 || value >= _pages.Length)
            return;

        CurrentPage = _pages[value];

        // The "back to analytics" button only makes sense right after a chart click.
        // Leaving the document list (to any page) ends that.
        if (value != 0)
            _documents.CameFromAnalytics = false;

        // Let the page know it was opened (e.g. Tags loads its list the first time)
        if (CurrentPage is INavigationAware page)
            page.OnNavigatedTo();
    }
}