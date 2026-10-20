using System.Windows;
using PaperlessDesktop.Services;

namespace PaperlessDesktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var config = AppConfig.Load();
            var api = new PaperlessApiClient(config);
            new MainWindow(api).Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The application could not start:\n\n{ex.Message}",
                            "Paperless Desktop", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}