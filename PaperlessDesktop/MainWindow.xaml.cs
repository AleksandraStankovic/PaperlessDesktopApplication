using System.Windows;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop;

public partial class MainWindow : Window
{
    private readonly PaperlessApiClient _api;

    public MainWindow(PaperlessApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        Spinner.Visibility = Visibility.Visible;
        ResultText.Text = "Connecting...";

        try
        {
            // Run both requests in parallel
            var tagsTask = _api.GetAllTagsAsync();
            var docsTask = _api.GetDocumentsAsync(new DocumentQuery { PageSize = 5 });
            await Task.WhenAll(tagsTask, docsTask);

            List<Tag> tags = await tagsTask;
            PagedResult<Document> docs = await docsTask;

            var tagNames = tags.ToDictionary(t => t.Id, t => t.Name);

            var lines = docs.Results.Select(d =>
            {
                string tagText = string.Join(", ", d.TagIds.Select(id => tagNames.GetValueOrDefault(id, "?")));
                return $"• {d.Title}  ({d.Created:dd.MM.yyyy})  [{tagText}]";
            });

            ResultText.Text = $"✅ Connected! {docs.Count} documents, {tags.Count} tags.\n\n" +
                              string.Join("\n", lines);
        }
        catch (ApiException ex)
        {
            ResultText.Text = $"❌ {ex.Message}";
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