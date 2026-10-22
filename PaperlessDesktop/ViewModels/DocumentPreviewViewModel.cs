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
        if (_previewFilePath is null)
            return;

        if (!_files.TryOpenWithDefaultApp(_previewFilePath))
            _notifications.ShowLocalized("Preview_OpenFailed");
    }

    private bool CanOpenExternally() => _previewFilePath is not null;

    private void SetPreviewFile(string? path)
    {
        _previewFilePath = path;
        OpenExternallyCommand.NotifyCanExecuteChanged();   // enable/disable the ↗ button
    }
}