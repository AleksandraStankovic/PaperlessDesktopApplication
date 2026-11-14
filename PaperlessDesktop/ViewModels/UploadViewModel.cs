using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;
using System.Security.Cryptography;

namespace PaperlessDesktop.ViewModels;

public enum UploadStepState
{
    Pending,
    Active,
    Done,
    Failed
}

public partial class UploadViewModel : ObservableObject, INavigationAware
{
    public static readonly string[] SupportedExtensions =
        { ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".gif", ".webp", ".txt" };

    public const long MaxFileSizeBytes = 50L * 1024 * 1024;
    private const int MaxTitleLength = 128;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan MaxProcessingWait = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan CancelWithoutTaskWait = TimeSpan.FromSeconds(45);

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

    public event EventHandler? DocumentUploaded;

    public event EventHandler? ShowDocumentsRequested;

    public string SupportedFormatsText => "PDF, JPG, PNG, TIFF, GIF, WebP, TXT";
    public string MaxFileSizeText => FormatSize(MaxFileSizeBytes);

    public string FileDialogFilter =>
        $"{_localization.Get("Upload_FileFilterName")}|{string.Join(";", SupportedExtensions.Select(e => "*" + e))}";

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDuplicateWarning))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    private string? _duplicateWarning;

    public bool HasDuplicateWarning => DuplicateWarning is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    private bool _isCheckingDuplicate;

    [ObservableProperty]
    private double _duplicateCheckProgress;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private bool _isDragRejected;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormEnabled))]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelUploadCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelUploadCommand))]
    private bool _isCancelling;

    [ObservableProperty]
    private double _uploadProgress;

    [ObservableProperty]
    private UploadStepState _sendStep;

    [ObservableProperty]
    private UploadStepState _readyStep;

    [ObservableProperty]
    private bool _showStatus;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _statusIsError;

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

    public static bool IsSupportedFile(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public void SetDragState(bool isOver, bool isRejected)
    {
        IsDragOver = isOver && !isRejected;
        IsDragRejected = isOver && isRejected;
    }

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

        string? replacedName = HasFile
            && !string.Equals(SelectedFilePath, path, StringComparison.OrdinalIgnoreCase)
            ? FileName : null;

        FileError = null;
        SelectedFilePath = path;
        FileSizeText = FormatSize(size);

        if (replacedName is not null)
            _notifications.Show(string.Format(_localization.Get("Upload_FileReplaced"), replacedName, FileName));

        _ = CheckForDuplicateAsync(path);

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

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        _titleTouched = true;
        OnPropertyChanged(nameof(TitleError));
        OnPropertyChanged(nameof(HasTitleError));

        if (SelectedFilePath is null || ValidateTitle() is not null)
            return;

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

        string? taskId = null;
        bool fileFullySent = false;
        IReadOnlySet<int> existingIds = new HashSet<int>();

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
            var uiProgress = new Progress<double>(fraction => UploadProgress = fraction * 100);
            var progress = new InlineProgress<double>(fraction =>
            {
                if (fraction >= 1)
                    fileFullySent = true;
                ((IProgress<double>)uiProgress).Report(fraction);
            });
            var request = new DocumentUploadRequest(
                SelectedFilePath, title,
                SelectedCorrespondent?.Id, SelectedDocumentType?.Id,
                SelectedTags.Select(t => t.Id).ToList());

            existingIds = await FindDocumentIdsWithTitleAsync(title, cts.Token);
            taskId = await _api.UploadDocumentAsync(request, progress, cts.Token);

            IsSending = false;
            StatusMessage = _localization.Get("Upload_Status_Processing");

            PaperlessTask? task = await WaitForProcessingAsync(taskId, title, existingIds, MaxProcessingWait, cts.Token);
            if (task is null)
            {
                SendStep = UploadStepState.Done;
                StatusMessage = _localization.Get("Upload_Status_StillProcessing");
            }
            else if (task.IsSuccess)
            {
                SendStep = UploadStepState.Done;
                ReadyStep = UploadStepState.Done;
                StatusMessage = string.Format(_localization.Get("Upload_Status_Done"), title);
                _notifications.Show(StatusMessage, NotificationKind.Success, _localization.Get("Upload_ShowDocuments"),
                                    () => ShowDocumentsRequested?.Invoke(this, EventArgs.Empty));
                DocumentUploaded?.Invoke(this, EventArgs.Empty);
                succeeded = true;
            }
            else
            {
                SendStep = UploadStepState.Failed;
                StatusIsError = true;
                StatusMessage = DescribeProcessingFailure(task.Result);
                _notifications.Show(StatusMessage, NotificationKind.Error);
            }
        }
        catch (Exception) when (cts.IsCancellationRequested)
        {
            await FinishCancelAsync(taskId, fileFullySent || taskId is not null, title, existingIds);
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
            IsCancelling = false;
            IsFinished = true;
            _uploadCts = null;
            cts.Dispose();
        }

        if (succeeded)
            Reset();
    }

    private bool CanUpload() => HasFile
                                && !IsBusy
                                && !IsCheckingDuplicate
                                && !HasDuplicateWarning
                                && ValidateTitle() is null;

    [RelayCommand(CanExecute = nameof(CanCancelUpload))]
    private void CancelUpload() => _uploadCts?.Cancel();

    private bool CanCancelUpload() => IsBusy && !IsCancelling;

    private async Task FinishCancelAsync(string? taskId, bool fileReachedServer,
                                         string title, IReadOnlySet<int> existingIds)
    {
        IsSending = false;

        if (fileReachedServer)
        {
            IsCancelling = true;
            StatusMessage = _localization.Get("Upload_Status_Cancelling");

            if (!await UndoUploadAsync(taskId, title, existingIds))
            {
                SendStep = UploadStepState.Failed;
                StatusIsError = true;
                StatusMessage = _localization.Get("Upload_Status_CancelUncertain");
                _notifications.Show(StatusMessage, NotificationKind.Error);
                DocumentUploaded?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        SendStep = UploadStepState.Pending;
        StatusMessage = _localization.Get("Upload_Status_Cancelled");
        if (fileReachedServer)
            _notifications.Show(StatusMessage);
    }

    private async Task<bool> UndoUploadAsync(string? taskId, string title, IReadOnlySet<int> existingIds)
    {
        try
        {
            TimeSpan maxWait = taskId is null ? CancelWithoutTaskWait : MaxProcessingWait;
            using var cts = new CancellationTokenSource(maxWait + TimeSpan.FromSeconds(30));

            PaperlessTask? task = await WaitForProcessingAsync(taskId, title, existingIds, maxWait, cts.Token);
            if (task is null)
                return false;
            if (!task.IsSuccess)
                return true;

            int? documentId = task.RelatedDocument;
            if (documentId is null)
            {
                var currentIds = await FindDocumentIdsWithTitleAsync(title, cts.Token);
                documentId = currentIds.Where(id => !existingIds.Contains(id))
                                       .Select(id => (int?)id)
                                       .FirstOrDefault();
            }
            if (documentId is not int uploadedId)
                return false;

            await _api.DeleteDocumentAsync(uploadedId, cts.Token);

            try
            {
                await _api.EmptyTrashForDocumentAsync(uploadedId, cts.Token);
            }
            catch (ApiException)
            {
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

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

    partial void OnTitleChanged(string value)
    {
        if (_settingTitleInternally)
            return;
        _titleTouched = true;
        _titleAutoFilled = false;
    }

    partial void OnTagToAddChanged(Tag? value)
    {
        if (value is null)
            return;
        if (!SelectedTags.Any(t => t.Id == value.Id))
            SelectedTags.Add(value);
        _ = ResetTagPickerAsync();
    }

    private async Task<PaperlessTask?> WaitForProcessingAsync(string? taskId, string title,
                                                           IReadOnlySet<int> existingIds, TimeSpan maxWait,
                                                           CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + maxWait;

        bool canReadTasks = taskId is not null;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, ct);

            try
            {
                if (canReadTasks)
                {
                    var task = await _api.GetTaskAsync(taskId!, ct);
                    if (task is { IsFinished: true })
                        return task;
                }
                else
                {
                    var currentIds = await FindDocumentIdsWithTitleAsync(title, ct);
                    int? newId = currentIds.Where(id => !existingIds.Contains(id))
                                           .Select(id => (int?)id)
                                           .FirstOrDefault();
                    if (newId is not null)
                        return new PaperlessTask { TaskId = taskId ?? "", Status = "SUCCESS", RelatedDocument = newId };
                }
            }
            catch (ApiException ex) when (canReadTasks && ex.Kind is ApiErrorKind.Forbidden
                                                                   or ApiErrorKind.Unauthorized
                                                                   or ApiErrorKind.NotFound)
            {
                canReadTasks = false;
            }
            catch (ApiException ex) when (ex.Kind is ApiErrorKind.Timeout or ApiErrorKind.Unreachable)
            {
            }
        }
        return null;
    }

    private async Task<HashSet<int>> FindDocumentIdsWithTitleAsync(string title, CancellationToken ct)
    {
        var query = new DocumentQuery { SearchText = title, Ordering = "-added", PageSize = 100 };
        var result = await _api.GetDocumentsAsync(query, ct);

        return result.Results
            .Where(d => string.Equals(d.Title, title, StringComparison.CurrentCultureIgnoreCase))
            .Select(d => d.Id)
            .ToHashSet();
    }

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
                string? checksum = await _api.GetOriginalChecksumAsync(doc.Id, cts.Token);
                if (!string.Equals(checksum, md5, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (SelectedFilePath == path)
                {
                    ClearFile();
                    DuplicateWarning = _localization.Get("Upload_DuplicateWarning");
                }
                return;
            }

            DuplicateCheckProgress = 100;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
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
        await Task.Yield();
        TagToAdd = null;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;
        public InlineProgress(Action<T> report) => _report = report;
        public void Report(T value) => _report(value);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        target.Clear();
        foreach (var item in list)
            target.Add(item);
    }
}