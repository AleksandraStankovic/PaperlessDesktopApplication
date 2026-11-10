using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;
using System.Security.Cryptography;


namespace PaperlessDesktop.ViewModels;

/// <summary>State of one step in the upload status display.</summary>
public enum UploadStepState
{
    Pending,   // not started
    Active,    // in progress (spinner)
    Done,      // finished (green check)
    Failed     // went wrong (red alert)
}

public partial class UploadViewModel : ObservableObject, INavigationAware
{
    // HCI problem 3: what's allowed is defined once, shown to the user and checked BEFORE uploading
    public static readonly string[] SupportedExtensions =
        { ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".gif", ".webp", ".txt" };

    public const long MaxFileSizeBytes = 50L * 1024 * 1024;   // 50 MB
    private const int MaxTitleLength = 128;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan MaxProcessingWait = TimeSpan.FromMinutes(3);

    private readonly PaperlessApiClient _api;
    private readonly LookupCache _lookups;
    private readonly NotificationService _notifications;
    private readonly LocalizationService _localization;

    private CancellationTokenSource? _uploadCts;
    private bool _titleTouched;
    private bool _titleAutoFilled;
    private bool _settingTitleInternally;

    public ObservableCollection<Correspondent> AvailableCorrespondents { get; } = new();
    public ObservableCollection<DocumentType> AvailableDocumentTypes { get; } = new();
    public ObservableCollection<Tag> AvailableTags { get; } = new();
    public ObservableCollection<Tag> SelectedTags { get; } = new();

    /// <summary>Raised when a document was uploaded AND is ready (the document list then refreshes).</summary>
    public event EventHandler? DocumentUploaded;

    public string SupportedFormatsText => "PDF, JPG, PNG, TIFF, GIF, WebP, TXT";
    public string MaxFileSizeText => FormatSize(MaxFileSizeBytes);

    /// <summary>File picker filter: only supported types are offered (prevents the error, H5).</summary>
    public string FileDialogFilter =>
        $"{_localization.Get("Upload_FileFilterName")}|{string.Join(";", SupportedExtensions.Select(e => "*" + e))}";

    // ---------- Selected file ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(FileName))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    private string? _selectedFilePath;
    private CancellationTokenSource? _duplicateCts;

    public bool HasFile => SelectedFilePath is not null;
    public string FileName => SelectedFilePath is null ? "" : Path.GetFileName(SelectedFilePath);

    [ObservableProperty]
    private string _fileSizeText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFileError))]
    private string? _fileError;

    public bool HasFileError => FileError is not null;

    // ---------- Duplicate check (HCI problem 3: warn BEFORE uploading) ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDuplicateWarning))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    private string? _duplicateWarning;

    public bool HasDuplicateWarning => DuplicateWarning is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    private bool _isCheckingDuplicate;

    /// <summary>How far the duplicate check has got (0–100), shown as a progress line under the file.</summary>
    [ObservableProperty]
    private double _duplicateCheckProgress;

    // ---------- Drag & drop visual state ----------

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private bool _isDragRejected;

    // ---------- Metadata ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleError))]
    [NotifyPropertyChangedFor(nameof(HasTitleError))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    private string _title = "";

    public string? TitleError => _titleTouched ? ValidateTitle() : null;
    public bool HasTitleError => TitleError is not null;

    [ObservableProperty]
    private Correspondent? _selectedCorrespondent;

    [ObservableProperty]
    private DocumentType? _selectedDocumentType;

    [ObservableProperty]
    private Tag? _tagToAdd;

    // ---------- Upload status (HCI problem 2): two simple steps ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormEnabled))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearFileCommand))]
    private bool _isBusy;

    /// <summary>True while the file is being sent (the only time cancelling is possible).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelUploadCommand))]
    private bool _isSending;

    [ObservableProperty]
    private double _uploadProgress;   // 0 - 100

    /// <summary>Step 1: "Adding the document" (sending + server preparation).</summary>
    [ObservableProperty]
    private UploadStepState _sendStep;

    /// <summary>Step 2: "Document is ready".</summary>
    [ObservableProperty]
    private UploadStepState _readyStep;

    [ObservableProperty]
    private bool _showStatus;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _statusIsError;

    /// <summary>Upload finished (successfully or not) - offer "upload another".</summary>
    [ObservableProperty]
    private bool _isFinished;

    public bool IsFormEnabled => !IsBusy;

    public UploadViewModel(PaperlessApiClient api, LookupCache lookups,
                           NotificationService notifications, LocalizationService localization)
    {
        _api = api;
        _lookups = lookups;
        _notifications = notifications;
        _localization = localization;
    }

    public void OnNavigatedTo() => _ = LoadOptionsAsync();

    // ---------- Called by the View (file picker / drag & drop) ----------

    public static bool IsSupportedFile(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public void SetDragState(bool isOver, bool isRejected)
    {
        IsDragOver = isOver && !isRejected;
        IsDragRejected = isOver && isRejected;
    }

    /// <summary>
    /// The same method is used for the file picker AND drag & drop,
    /// so both behave and look exactly the same (H4 consistency).
    /// </summary>
    public void SelectFile(string path, bool multipleDropped = false)
    {
        if (IsBusy)
            return;

        if (multipleDropped)
            _notifications.ShowLocalized("Upload_OnlyOneFile");

        string? error = ValidateFile(path, out long size);
        if (error is not null)
        {
            FileError = error;
            DuplicateWarning = null;
            return;
        }

        FileError = null;
        SelectedFilePath = path;
        FileSizeText = FormatSize(size);
        _ = CheckForDuplicateAsync(path);

        // Suggest the file name as title (unless the user already typed their own)
        if (string.IsNullOrWhiteSpace(Title) || _titleAutoFilled)
        {
            string suggested = Path.GetFileNameWithoutExtension(path);
            if (suggested.Length > MaxTitleLength)
                suggested = suggested[..MaxTitleLength];
            SetTitleInternally(suggested);
            _titleAutoFilled = true;
        }

        if (IsFinished)
            ClearStatus();
    }

    // ---------- Commands ----------

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        _titleTouched = true;
        OnPropertyChanged(nameof(TitleError));
        OnPropertyChanged(nameof(HasTitleError));

        if (SelectedFilePath is null || ValidateTitle() is not null)
            return;

        // The file may have been moved or deleted since it was selected
        string? fileError = ValidateFile(SelectedFilePath, out _);
        if (fileError is not null)
        {
            FileError = fileError;
            return;
        }

        var cts = new CancellationTokenSource();
        _uploadCts = cts;
        string title = Title.Trim();
        bool succeeded = false;

        IsBusy = true;
        IsSending = true;
        IsFinished = false;
        ShowStatus = true;
        StatusIsError = false;
        UploadProgress = 0;
        SendStep = UploadStepState.Active;
        ReadyStep = UploadStepState.Pending;
        StatusMessage = _localization.Get("Upload_Status_Sending");

        try
        {
            // Send the file (with a real progress bar)
            var progress = new Progress<double>(fraction => UploadProgress = fraction * 100);
            var request = new DocumentUploadRequest(
                SelectedFilePath, title,
                SelectedCorrespondent?.Id, SelectedDocumentType?.Id,
                SelectedTags.Select(t => t.Id).ToList());

            // Remember which documents with this title already exist, so plan B only accepts a NEW one
            var existingIds = await FindDocumentIdsWithTitleAsync(title, cts.Token);
            string taskId = await _api.UploadDocumentAsync(request, progress, cts.Token);

            // The file is on the server. It still has to be prepared there,
            // but the user doesn't need those details - step 1 simply stays active (H8).
            IsSending = false;
            StatusMessage = _localization.Get("Upload_Status_Processing");

            PaperlessTask? task = await WaitForProcessingAsync(taskId, title, existingIds, cts.Token);
            if (task is null)
            {
                // Took too long - be honest, don't pretend it failed
                SendStep = UploadStepState.Done;
                StatusMessage = _localization.Get("Upload_Status_StillProcessing");
            }
            else if (task.IsSuccess)
            {
                SendStep = UploadStepState.Done;
                ReadyStep = UploadStepState.Done;
                StatusMessage = string.Format(_localization.Get("Upload_Status_Done"), title);
                _notifications.Show(StatusMessage);   // also visible if the user went to another page
                DocumentUploaded?.Invoke(this, EventArgs.Empty);
                succeeded = true;
            }
            else
            {
                SendStep = UploadStepState.Failed;
                StatusIsError = true;
                StatusMessage = DescribeProcessingFailure(task.Result);
                _notifications.Show(StatusMessage);
            }
        }
        catch (OperationCanceledException)
        {
            SendStep = UploadStepState.Pending;
            StatusMessage = _localization.Get("Upload_Status_Cancelled");
        }
        catch (IOException)
        {
            SendStep = UploadStepState.Failed;
            StatusIsError = true;
            StatusMessage = _localization.Get("Upload_Error_CannotRead");
        }
        catch (Exception ex)
        {
            SendStep = UploadStepState.Failed;
            StatusIsError = true;
            string reason = ex is ApiException api
                ? _localization.Get($"Error_{api.Kind}")
                : _localization.Get("Error_Unexpected");
            StatusMessage = string.Format(_localization.Get("Upload_Error_Send"), reason);
        }
        finally
        {
            IsBusy = false;
            IsSending = false;
            IsFinished = true;
            _uploadCts = null;
            cts.Dispose();
        }

        // Success is confirmed by the notification, so the page starts over completely,
        // ready for the next document
        if (succeeded)
            Reset();
    }

    // HCI problem 3: a duplicate can't be uploaded (the server would reject it anyway),
    // and uploading waits until the duplicate check has finished (H5 error prevention)
    private bool CanUpload() => HasFile
                                && !IsBusy
                                && !IsCheckingDuplicate
                                && !HasDuplicateWarning
                                && ValidateTitle() is null;

    /// <summary>HCI problem 2: the user CAN stop the upload while it's being sent (H3).</summary>
    [RelayCommand(CanExecute = nameof(IsSending))]
    private void CancelUpload() => _uploadCts?.Cancel();

    [RelayCommand(CanExecute = nameof(IsFormEnabled))]
    private void ClearFile()
    {
        CancelDuplicateCheck();
        SelectedFilePath = null;
        FileSizeText = "";
        FileError = null;
        if (_titleAutoFilled)
            SetTitleInternally("");
    }

    /// <summary>"Reset" / "Upload another document": back to an empty form.</summary>
    [RelayCommand(CanExecute = nameof(IsFormEnabled))]
    private void Reset()
    {
        ClearFile();
        SetTitleInternally("");
        _titleTouched = false;
        _titleAutoFilled = false;
        OnPropertyChanged(nameof(TitleError));
        OnPropertyChanged(nameof(HasTitleError));

        SelectedCorrespondent = null;
        SelectedDocumentType = null;
        SelectedTags.Clear();
        ClearStatus();
    }

    [RelayCommand]
    private void RemoveTag(Tag? tag)
    {
        if (tag is not null)
            SelectedTags.Remove(tag);
    }

    // ---------- Reactions to property changes ----------

    partial void OnTitleChanged(string value)
    {
        if (_settingTitleInternally)
            return;
        _titleTouched = true;       // the user typed - from now on show validation messages
        _titleAutoFilled = false;   // and don't overwrite their title with the next file name
    }

    partial void OnTagToAddChanged(Tag? value)
    {
        if (value is null)
            return;
        if (!SelectedTags.Any(t => t.Id == value.Id))
            SelectedTags.Add(value);
        _ = ResetTagPickerAsync();
    }

    // ---------- Helpers ----------



    /// <summary>
    /// Plan B: finds a document with exactly this title that was added after the upload started.
    /// Returns its ID, or null if it isn't there (yet).
    /// </summary>
    private async Task<PaperlessTask?> WaitForProcessingAsync(string taskId, string title,
                                                           IReadOnlySet<int> existingIds, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + MaxProcessingWait;

        // Plan A: ask the tasks endpoint (also tells us about duplicates / failures).
        // Some servers don't let ordinary users read tasks - then we switch to plan B.
        bool canReadTasks = true;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, ct);

            try
            {
                if (canReadTasks)
                {
                    var task = await _api.GetTaskAsync(taskId, ct);
                    if (task is { IsFinished: true })
                        return task;
                }
                else
                {
                    // Plan B: a document with this title that did NOT exist before the upload
                    var currentIds = await FindDocumentIdsWithTitleAsync(title, ct);
                    int? newId = currentIds.Where(id => !existingIds.Contains(id))
                                           .Select(id => (int?)id)
                                           .FirstOrDefault();
                    if (newId is not null)
                        return new PaperlessTask { TaskId = taskId, Status = "SUCCESS", RelatedDocument = newId };
                }
            }
            catch (ApiException ex) when (canReadTasks && ex.Kind is ApiErrorKind.Forbidden
                                                                   or ApiErrorKind.Unauthorized
                                                                   or ApiErrorKind.NotFound)
            {
                canReadTasks = false;   // tasks endpoint not allowed here - use plan B from now on
            }
            catch (ApiException ex) when (ex.Kind is ApiErrorKind.Timeout or ApiErrorKind.Unreachable)
            {
                // short network hiccup - keep asking
            }
        }
        return null;   // still not finished after 3 minutes
    }

    /// <summary>IDs of all documents whose title is exactly this title (case-insensitive).</summary>
    private async Task<HashSet<int>> FindDocumentIdsWithTitleAsync(string title, CancellationToken ct)
    {
        var query = new DocumentQuery { SearchText = title, Ordering = "-added", PageSize = 100 };
        var result = await _api.GetDocumentsAsync(query, ct);

        return result.Results
            .Where(d => string.Equals(d.Title, title, StringComparison.CurrentCultureIgnoreCase))
            .Select(d => d.Id)
            .ToHashSet();
    }

    /// <summary>
    /// HCI problem 3: right after choosing a file, check whether the SAME file is already in Paperless,
    /// so the user knows BEFORE uploading (H5 error prevention).
    /// </summary>
    private async Task CheckForDuplicateAsync(string path)
    {
        _duplicateCts?.Cancel();
        var cts = new CancellationTokenSource();
        _duplicateCts = cts;

        DuplicateWarning = null;
        DuplicateCheckProgress = 0;
        IsCheckingDuplicate = true;
        try
        {
            // Reading the file is most of the work (0–80 %), asking the server is the rest
            var progress = new Progress<double>(p =>
            {
                if (_duplicateCts == cts)
                    DuplicateCheckProgress = p * 80;
            });
            string md5 = await ComputeMd5Async(path, progress, cts.Token);
            var candidates = await _api.FindDocumentsByChecksumAsync(md5, cts.Token);
            DuplicateCheckProgress = 90;

            foreach (var doc in candidates.Results)
            {
                // Double-check through the metadata, so a server that ignores the filter can't cause false warnings
                string? checksum = await _api.GetOriginalChecksumAsync(doc.Id, cts.Token);
                if (!string.Equals(checksum, md5, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (SelectedFilePath == path)   // the user may have picked another file meanwhile
                {
                    // The duplicate is removed automatically, so the user only has to pick another file
                    ClearFile();
                    DuplicateWarning = _localization.Get("Upload_DuplicateWarning");
                }
                return;
            }

            DuplicateCheckProgress = 100;
        }
        catch (OperationCanceledException)
        {
            // another file was chosen - nothing to report
        }
        catch (Exception)
        {
            // The check is only a convenience - if it fails, uploading still works normally
        }
        finally
        {
            if (_duplicateCts == cts)
            {
                _duplicateCts = null;
                IsCheckingDuplicate = false;
            }
            cts.Dispose();
        }
    }

    private static async Task<string> ComputeMd5Async(string path, IProgress<double> progress, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        long total = Math.Max(stream.Length, 1);
        long read = 0;
        byte[] buffer = new byte[1024 * 1024];
        int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0)
        {
            md5.AppendData(buffer, 0, n);
            read += n;
            progress.Report((double)read / total);
        }

        return Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant();
    }

    private void CancelDuplicateCheck()
    {
        _duplicateCts?.Cancel();
        DuplicateWarning = null;
        IsCheckingDuplicate = false;
    }

    private string DescribeProcessingFailure(string? serverMessage)
    {
        // HCI: duplicates explained in plain words instead of the server's technical message (H9)
        if (serverMessage is not null && serverMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return _localization.Get("Upload_Error_Duplicate");

        return string.Format(_localization.Get("Upload_Error_Processing"), serverMessage ?? "—");
    }

    private string? ValidateFile(string path, out long size)
    {
        size = 0;

        if (!IsSupportedFile(path))
        {
            string extension = Path.GetExtension(path);
            return string.Format(_localization.Get("Upload_Error_Type"),
                string.IsNullOrEmpty(extension) ? "?" : extension.ToLowerInvariant(),
                SupportedFormatsText);
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return _localization.Get("Upload_Error_NotFound");
            size = info.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return _localization.Get("Upload_Error_NotFound");
        }

        if (size == 0)
            return _localization.Get("Upload_Error_Empty");
        if (size > MaxFileSizeBytes)
            return string.Format(_localization.Get("Upload_Error_TooLarge"), FormatSize(size), MaxFileSizeText);

        return null;
    }

    private string? ValidateTitle()
    {
        string title = Title?.Trim() ?? "";
        if (title.Length == 0)
            return _localization.Get("Upload_Error_TitleRequired");
        if (title.Length > MaxTitleLength)
            return _localization.Get("Upload_Error_TitleTooLong");
        return null;
    }

    private void SetTitleInternally(string value)
    {
        _settingTitleInternally = true;
        Title = value;
        _settingTitleInternally = false;
    }

    private void ClearStatus()
    {
        ShowStatus = false;
        IsFinished = false;
        StatusMessage = null;
        StatusIsError = false;
        UploadProgress = 0;
        SendStep = UploadStepState.Pending;
        ReadyStep = UploadStepState.Pending;
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            if (!_lookups.IsLoaded)
                await _lookups.LoadAsync();

            // Remember current choices, refill the lists, then restore the choices
            int? correspondentId = SelectedCorrespondent?.Id;
            int? documentTypeId = SelectedDocumentType?.Id;
            var tagIds = SelectedTags.Select(t => t.Id).ToList();

            var comparer = StringComparer.CurrentCultureIgnoreCase;
            Replace(AvailableCorrespondents, _lookups.Correspondents.OrderBy(c => c.Name, comparer));
            Replace(AvailableDocumentTypes, _lookups.DocumentTypes.OrderBy(t => t.Name, comparer));
            Replace(AvailableTags, _lookups.Tags.OrderBy(t => t.Name, comparer));

            SelectedCorrespondent = AvailableCorrespondents.FirstOrDefault(c => c.Id == correspondentId);
            SelectedDocumentType = AvailableDocumentTypes.FirstOrDefault(t => t.Id == documentTypeId);
            Replace(SelectedTags, AvailableTags.Where(t => tagIds.Contains(t.Id)));
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
        }
    }

    private async Task ResetTagPickerAsync()
    {
        await Task.Yield();   // let the ComboBox finish handling the click first
        TagToAdd = null;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();   // copy first, in case items comes from target itself
        target.Clear();
        foreach (var item in list)
            target.Add(item);
    }
}