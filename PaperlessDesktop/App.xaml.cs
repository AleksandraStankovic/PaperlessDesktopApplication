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

        // Safety net: from here on, no unexpected error can close the app
        RegisterGlobalErrorHandlers();
        Log.DeleteOldFiles();
        Log.Info("Application starting");

        base.OnStartup(e);

        try
        {
            // ----- Services (created once, shared by everyone) -----
            var config = AppConfig.Load();
            var api = new PaperlessApiClient(config);
            var lookups = new LookupCache(api);

            var files = new DocumentFileService(api);
            files.CleanUp();   // remove preview files left from the previous session

            // Saved user preferences (language, theme, color) - applied before the window opens
            var settingsService = new SettingsService();
            var userSettings = settingsService.Load();

            var localization = new LocalizationService();
            localization.SetLanguage(userSettings.Language);

            var themeService = new ThemeService();
            themeService.Apply(userSettings.IsDarkTheme,
                ThemeService.ParseColor(userSettings.PrimaryColor, ThemeService.DefaultPrimary));

            var notifications = new NotificationService(localization);
            _notifications = notifications;   // the global error handler uses it for friendly messages
            var dialogs = new DialogService();

            // ----- ViewModels -----
            var documentsViewModel = new DocumentsViewModel(api, lookups, files, notifications);
            var uploadViewModel = new UploadViewModel(api, lookups, notifications, localization);
            var tagsViewModel = new TagsViewModel(api, dialogs, notifications, localization);
            var analyticsViewModel = new AnalyticsViewModel(api, lookups, notifications, localization);

            // When tags change, the document list refreshes tag names, colors and filter options
            tagsViewModel.TagsChanged += (_, _) => _ = documentsViewModel.ReloadLookupsAsync();
            tagsViewModel.TagsChanged += (_, _) => analyticsViewModel.Invalidate();
            uploadViewModel.DocumentUploaded += (_, _) => _ = documentsViewModel.ReloadLookupsAsync();
            uploadViewModel.DocumentUploaded += (_, _) => analyticsViewModel.Invalidate();
           

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
            documentsViewModel.BackToAnalyticsRequested += (_, _) => mainViewModel.ShowAnalytics();
            documentsViewModel.AddDocumentRequested += (_, _) => mainViewModel.ShowUpload();
            uploadViewModel.ShowDocumentsRequested += (_, _) => mainViewModel.ShowDocuments();

            // ----- Window -----
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

    // ---------- Global error handling ----------

    private void RegisterGlobalErrorHandlers()
    {
        // 1. Errors on the UI thread (buttons, bindings, commands, most of our code)
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 2. Errors in background tasks that nobody awaited
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 3. Errors on other threads - can't be stopped, but at least they're recorded
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled exception on the UI thread", e.Exception);
        e.Handled = true;   // ← this is what keeps the app running instead of closing
        ShowUnexpectedErrorMessage();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error("Unobserved exception in a background task", e.Exception);
        e.SetObserved();    // mark as handled
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("Fatal unhandled exception", e.ExceptionObject as Exception);
    }

    private void ShowUnexpectedErrorMessage()
    {
        // If something fails repeatedly (e.g. while redrawing), show the message at most every 5 seconds
        if (DateTime.Now - _lastUnexpectedErrorShown < TimeSpan.FromSeconds(5))
            return;
        _lastUnexpectedErrorShown = DateTime.Now;

        if (_notifications is not null)
            _notifications.ShowLocalized("Error_UnexpectedRecovered", NotificationKind.Error);
        else
            MessageBox.Show("An unexpected error occurred. The details were saved to the log.",
                            "Paperless Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// Chips: replaces MaterialDesign's fixed grey circle behind the ✕ with
    /// "ChipDeleteButtonTemplate" (a darker shade of the chip's own color).
    /// Hooked up by the Chip style in App.xaml.
    /// </summary>
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
