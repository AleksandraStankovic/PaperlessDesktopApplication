using System.Reflection;
using System.Threading;
using MaterialDesignThemes.Wpf;

namespace PaperlessDesktop.Services;

public enum NotificationKind
{
    Info,
    Success,
    Error
}

public sealed record NotificationMessage(string Text, NotificationKind Kind)
{
    public bool IsSuccess => Kind == NotificationKind.Success;
    public bool IsError => Kind == NotificationKind.Error;

    public override string ToString() => Text;
}

public class NotificationService
{
    private readonly LocalizationService _localization;

    private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan MessageWithActionDuration = TimeSpan.FromSeconds(5);

    private static readonly FieldInfo? CloseEventField =
        typeof(SnackbarMessageQueue).GetField("_closeSnackbarEvent", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? QueueLockField =
        typeof(SnackbarMessageQueue).GetField("_snackbarMessagesLock", BindingFlags.Instance | BindingFlags.NonPublic);

    public SnackbarMessageQueue MessageQueue { get; } = new(MessageDuration);

    public NotificationService(LocalizationService localization)
    {
        _localization = localization;
    }

    public void Show(string message, NotificationKind kind = NotificationKind.Info)
        => MessageQueue.Enqueue(new NotificationMessage(message, kind));

    public void ShowLocalized(string key, NotificationKind kind = NotificationKind.Info)
        => Show(_localization.Get(key), kind);

    public void ShowSuccess(string message) => Show(message, NotificationKind.Success);

    public void ShowSuccessLonger(string message)
        => MessageQueue.Enqueue<object?>(new NotificationMessage(message, NotificationKind.Success), null, null, null,
                                         false, false, MessageWithActionDuration);

    public void Show(string message, NotificationKind kind, string actionText, Action action)
        => MessageQueue.Enqueue<object?>(new NotificationMessage(message, kind), actionText, _ => action(), null,
                                         false, false, MessageWithActionDuration);

    public void DismissCurrent()
    {
        object? queueLock = QueueLockField?.GetValue(MessageQueue);
        if (CloseEventField is null || queueLock is null)
        {
            MessageQueue.Clear();
            return;
        }

        lock (queueLock)
        {
            try
            {
                (CloseEventField.GetValue(MessageQueue) as ManualResetEvent)?.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public void ShowError(Exception ex)
    {
        Log.Warning("Error shown to the user", ex);

        string key = ex is ApiException apiEx ? $"Error_{apiEx.Kind}" : "Error_Unexpected";
        ShowLocalized(key, NotificationKind.Error);
    }
}