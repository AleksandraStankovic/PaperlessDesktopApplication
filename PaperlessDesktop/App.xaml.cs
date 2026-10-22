using System.Windows;
using PaperlessDesktop.Services;
using PaperlessDesktop.ViewModels;

namespace PaperlessDesktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // ----- Services (created once, shared by everyone) -----
            var config = AppConfig.Load();
            var api = new PaperlessApiClient(config);
            var lookups = new LookupCache(api);

            var files = new DocumentFileService(api);
            files.CleanUp();   // remove preview files left from the previous session

            var localization = new LocalizationService();
            localization.SetLanguage("sr");   // Phase 9: load from saved settings

            var notifications = new NotificationService(localization);
            var dialogs = new DialogService();

            // ----- ViewModels -----
            var documentsViewModel = new DocumentsViewModel(api, lookups, files, notifications);
            var tagsViewModel = new TagsViewModel(api, dialogs, notifications, localization);

            // When tags change, the document list refreshes tag names, colors and filter options
            tagsViewModel.TagsChanged += (_, _) => _ = documentsViewModel.ReloadLookupsAsync();

            var mainViewModel = new MainViewModel(
                documentsViewModel,
                new UploadViewModel(),
                tagsViewModel,
                new SettingsViewModel(localization),
                notifications);

            // ----- Window -----
            new MainWindow(mainViewModel).Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The application could not start:\n\n{ex.Message}",
                            "Paperless Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}