using System.Reflection;
using System.Threading;
using MaterialDesignThemes.Wpf;

namespace PaperlessDesktop.Services;

/// <summary>What a notification is about - decides its color and icon (green = success, red = error).</summary>
public enum NotificationKind
{
    Info,
    Success,
    Error
}

/// <summary>One snackbar message. Shown by the NotificationMessage template in App.xaml.</summary>
public sealed record NotificationMessage(string Text, NotificationKind Kind)
{
    public bool IsSuccess => Kind == NotificationKind.Success;
    public bool IsError => Kind == NotificationKind.Error;

    public override string ToString() => Text;
}

public class NotificationService
{
    private readonly LocalizationService _localization;

    // How long a message stays on screen. Kept short so notifications don't get in the way;
    // the timer pauses while the mouse is over the message, and the ✕ closes it right away.
    private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(3);

    /// <summary>A message with a button ("Prikaži dokumente", "Poništi") stays longer, so there is time to click it.</summary>
    private static readonly TimeSpan MessageWithActionDuration = TimeSpan.FromSeconds(5);

    // MaterialDesign's queue has no public "close only the current message" (Clear() would also throw
    // away messages still waiting in line). Its private close signal does exactly that and is what
    // Clear() uses internally - read via reflection, checked against MaterialDesignThemes 5.3.2.
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

    /// <summary>Message with a button (e.g. "Prikaži dokumente"). Stays a bit longer, so there is time to click it.</summary>
    public void Show(string message, NotificationKind kind, string actionText, Action action)
        => MessageQueue.Enqueue<object?>(new NotificationMessage(message, kind), actionText, _ => action(), null,
                                         false, false, MessageWithActionDuration);

    /// <summary>
    /// The ✕ on a notification: closes the message on screen now (without running its action),
    /// the next waiting message (if any) is shown as usual.
    /// </summary>
    public void DismissCurrent()
    {
        object? queueLock = QueueLockField?.GetValue(MessageQueue);
        if (CloseEventField is null || queueLock is null)
        {
            MessageQueue.Clear();   // fallback if a MaterialDesign update renamed the fields
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
                // The message was just closing on its own - nothing to do
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