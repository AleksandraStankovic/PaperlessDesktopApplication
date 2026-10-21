using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ObservableObject[] _pages;

    public SnackbarMessageQueue MessageQueue { get; }

    [ObservableProperty]
    private ObservableObject? _currentPage;

    [ObservableProperty]
    private int _selectedPageIndex;

    public MainViewModel(
        DocumentsViewModel documents,
        UploadViewModel upload,
        TagsViewModel tags,
        SettingsViewModel settings,
        NotificationService notifications)
    {
        _pages = new ObservableObject[] { documents, upload, tags, settings };
        CurrentPage = documents;
        MessageQueue = notifications.MessageQueue;
    }

    partial void OnSelectedPageIndexChanged(int value)
    {
        if (value >= 0 && value < _pages.Length)
            CurrentPage = _pages[value];
    }
}