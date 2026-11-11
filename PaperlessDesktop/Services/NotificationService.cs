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

    public SnackbarMessageQueue MessageQueue { get; } = new(TimeSpan.FromSeconds(4));

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
                                         false, false, TimeSpan.FromSeconds(8));

    public void ShowError(Exception ex)
    {
        Log.Warning("Error shown to the user", ex);

        string key = ex is ApiException apiEx ? $"Error_{apiEx.Kind}" : "Error_Unexpected";
        ShowLocalized(key, NotificationKind.Error);
    }
}