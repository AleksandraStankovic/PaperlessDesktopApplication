using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;

namespace PaperlessDesktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        Spinner.Visibility = Visibility.Visible;
        ResultText.Text = "Connecting...";

        try
        {
            // 1. Read server URL and token from appsettings.json
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            string configJson = await File.ReadAllTextAsync(configPath);
            using var config = JsonDocument.Parse(configJson);
            string serverUrl = config.RootElement.GetProperty("ServerUrl").GetString()!;
            string token = config.RootElement.GetProperty("ApiToken").GetString()!;

            // 2. Call the Paperless API
            using var http = new HttpClient
            {
                BaseAddress = new Uri(serverUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);

            string response = await http.GetStringAsync("/api/documents/?page_size=5");

            // 3. Show the result
            using var data = JsonDocument.Parse(response);
            int count = data.RootElement.GetProperty("count").GetInt32();
            var titles = data.RootElement.GetProperty("results")
                                         .EnumerateArray()
                                         .Select(d => "• " + d.GetProperty("title").GetString());

            ResultText.Text = $"✅ Connected! The server has {count} documents.\n\n" +
                              "First few:\n" + string.Join("\n", titles);
        }
        catch (FileNotFoundException)
        {
            ResultText.Text = "❌ appsettings.json was not found next to the .exe. " +
                              "Check that 'CopyToOutputDirectory' is set in the .csproj.";
        }
        catch (TaskCanceledException)
        {
            ResultText.Text = "❌ The server did not respond in 15 seconds.";
        }
        catch (HttpRequestException ex)
        {
            ResultText.Text = $"❌ Could not reach the server: {ex.Message}";
        }
        catch (Exception ex)
        {
            ResultText.Text = $"❌ Unexpected error: {ex.Message}";
        }
        finally
        {
            Spinner.Visibility = Visibility.Collapsed;
            TestButton.IsEnabled = true;
        }
    }
}