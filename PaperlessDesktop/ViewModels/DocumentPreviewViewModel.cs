using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

/// <summary>The side panel: selected document's metadata and file preview.</summary>
public partial class DocumentPreviewViewModel : ObservableObject
{
    // WebView2 must never get a null address, so "no preview" is an empty page
    private static readonly Uri BlankPage = new("about:blank");

    private readonly DocumentFileService _files;
    private readonly NotificationService _notifications;

    private CancellationTokenSource? _loadCts;
    private string? _previewFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private DocumentItemViewModel? _document;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    private bool _isLoadingPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    private Uri _previewUri = BlankPage;

    [ObservableProperty]
    private bool _previewFailed;

    public bool IsOpen => Document is not null;

    public bool ShowPreview => !IsLoadingPreview && PreviewUri != BlankPage;

    /// <summary>Raised when the user closes the panel (the list then clears its selection).</summary>
    public event EventHandler? CloseRequested;

    public DocumentPreviewViewModel(DocumentFileService files, NotificationService notifications)
    {
        _files = files;
        _notifications = notifications;
    }

    /// <summary>Shows a document in the panel, or closes the panel if item is null.</summary>
    public async Task ShowAsync(DocumentItemViewModel? item)
    {
        // Stop loading the previously selected document
        _loadCts?.Cancel();

        Document = item;
        PreviewUri = BlankPage;
        PreviewFailed = false;
        SetPreviewFile(null);

        if (item is null)
        {
            IsLoadingPreview = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoadingPreview = true;
        try
        {
            string path = await _files.GetPreviewFileAsync(item.Model, cts.Token);
            cts.Token.ThrowIfCancellationRequested();   // user selected another document meanwhile

            SetPreviewFile(path);
            PreviewUri = new Uri(path);
        }
        catch (OperationCanceledException)
        {
            // Another document was selected - nothing to report
        }
        catch (Exception ex)
        {
            PreviewFailed = true;
            _notifications.ShowError(ex);
        }
        finally
        {
            if (_loadCts == cts)
            {
                _loadCts = null;
                IsLoadingPreview = false;
            }
            cts.Dispose();
        }
    }

    // ---------- Commands ----------

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanOpenExternally))]
    private void OpenExternally()
    {
        if (_previewFilePath is not null)
            OpenFile(_previewFilePath);
    }

    private bool CanOpenExternally() => _previewFilePath is not null;

    /// <summary>
    /// The ↗ button on a list row: opens that document in the default app without opening
    /// the preview panel. Uses the same downloaded file as the preview (cached, so a document
    /// that was already previewed opens immediately). While a file is downloading, the row
    /// buttons are disabled, so a double click doesn't open the document twice.
    /// </summary>
    [RelayCommand]
    private async Task OpenInDefaultAppAsync(DocumentItemViewModel? item)
    {
        if (item is null)
            return;

        try
        {
            string path = await _files.GetPreviewFileAsync(item.Model, CancellationToken.None);
            OpenFile(path);
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
        }
    }

    private void OpenFile(string path)
    {
        if (!_files.TryOpenWithDefaultApp(path))
            _notifications.ShowLocalized("Preview_OpenFailed", NotificationKind.Error);
    }

    private void SetPreviewFile(string? path)
    {
        _previewFilePath = path;
        OpenExternallyCommand.NotifyCanExecuteChanged();   // enable/disable the ↗ button
    }
}