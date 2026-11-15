using System.Windows;
using PaperlessDesktop.Services;
using PaperlessDesktop.ViewModels;
using System.Windows.Threading;

namespace PaperlessDesktop;

public partial class App : Application
{
    private NotificationService? _notifications;
    private DateTime _lastUnexpectedErrorShown = DateTime.MinValue;
    protected override void OnStartup(StartupEventArgs e)
    {

        RegisterGlobalErrorHandlers();
        Log.DeleteOldFiles();
        Log.Info("Application starting");

        base.OnStartup(e);

        try
        {
            var config = AppConfig.Load();
            var api = new PaperlessApiClient(config);
            var lookups = new LookupCache(api);

            var files = new DocumentFileService(api);
            files.CleanUp();

            var settingsService = new SettingsService();
            var userSettings = settingsService.Load();

            var localization = new LocalizationService();
            localization.SetLanguage(userSettings.Language);

            var themeService = new ThemeService();
            themeService.Apply(userSettings.IsDarkTheme,
                ThemeService.ParseColor(userSettings.PrimaryColor, ThemeService.DefaultPrimary));

            var notifications = new NotificationService(localization);
            _notifications = notifications;
            var dialogs = new DialogService();

            var documentsViewModel = new DocumentsViewModel(api, lookups, files, notifications, dialogs, localization);
            var uploadViewModel = new UploadViewModel(api, lookups, notifications, localization);
            var tagsViewModel = new TagsViewModel(api, dialogs, notifications, localization);
            var analyticsViewModel = new AnalyticsViewModel(api, lookups, notifications, localization);

            tagsViewModel.TagsChanged += (_, _) => _ = documentsViewModel.ReloadLookupsAsync();
            tagsViewModel.TagsChanged += (_, _) => analyticsViewModel.Invalidate();
            uploadViewModel.DocumentUploaded += (_, _) => _ = documentsViewModel.ReloadLookupsAsync();
            uploadViewModel.DocumentUploaded += (_, _) => analyticsViewModel.Invalidate();

            documentsViewModel.DocumentTagsEdited += (_, _) => analyticsViewModel.Invalidate();
            documentsViewModel.DocumentTagsEdited += (_, _) => tagsViewModel.Invalidate();

            documentsViewModel.DocumentDeleted += (_, _) => analyticsViewModel.Invalidate();
            documentsViewModel.DocumentDeleted += (_, _) => tagsViewModel.Invalidate();
           
            var mainViewModel = new MainViewModel(
                documentsViewModel,
                analyticsViewModel,
                uploadViewModel,
                tagsViewModel,
                new SettingsViewModel(localization, themeService, settingsService, userSettings, notifications),
                notifications);

            analyticsViewModel.FilterRequested += (_, chip) =>
            {
                documentsViewModel.ShowWithFilter(chip);
                mainViewModel.ShowDocuments();
            };
            tagsViewModel.ShowDocumentsRequested += (_, tag) =>
            {
                documentsViewModel.ShowWithFilter(FilterChip.ForTag(tag), fromTags: true);
                mainViewModel.ShowDocuments();
            };
            documentsViewModel.BackToAnalyticsRequested += (_, _) => mainViewModel.ShowAnalytics();
            documentsViewModel.BackToTagsRequested += (_, _) => mainViewModel.ShowTags();
            documentsViewModel.AddDocumentRequested += (_, _) => mainViewModel.ShowUpload();
            uploadViewModel.ShowDocumentsRequested += (_, _) => mainViewModel.ShowDocuments();

            new MainWindow(mainViewModel).Show();
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed", ex);
            MessageBox.Show($"The application could not start:\n\n{ex.Message}",
                            "Paperless Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("Application exiting");
        base.OnExit(e);
    }

    private void RegisterGlobalErrorHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled exception on the UI thread", e.Exception);
        e.Handled = true;
        ShowUnexpectedErrorMessage();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error("Unobserved exception in a background task", e.Exception);
        e.SetObserved();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("Fatal unhandled exception", e.ExceptionObject as Exception);
    }

    private void ShowUnexpectedErrorMessage()
    {
        if (DateTime.Now - _lastUnexpectedErrorShown < TimeSpan.FromSeconds(5))
            return;
        _lastUnexpectedErrorShown = DateTime.Now;

        if (_notifications is not null)
            _notifications.ShowLocalized("Error_UnexpectedRecovered", NotificationKind.Error);
        else
            MessageBox.Show("An unexpected error occurred. The details were saved to the log.",
                            "Paperless Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Chip_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MaterialDesignThemes.Wpf.Chip chip)
            return;

        chip.ApplyTemplate();
        if (chip.Template?.FindName("PART_DeleteButton", chip) is System.Windows.Controls.Button deleteButton
            && Current.TryFindResource("ChipDeleteButtonTemplate") is System.Windows.Controls.ControlTemplate template
            && deleteButton.Template != template)
        {
            deleteButton.Template = template;
        }
    }
}
