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

            // ----- ViewModels -----
            var mainViewModel = new MainViewModel(
                new DocumentsViewModel(api, lookups, files, notifications),
                new UploadViewModel(),
                new TagsViewModel(),
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