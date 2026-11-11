using MaterialDesignThemes.Wpf;

namespace PaperlessDesktop.Services;

public class NotificationService
{
    private readonly LocalizationService _localization;

    public SnackbarMessageQueue MessageQueue { get; } = new(TimeSpan.FromSeconds(4));

    public NotificationService(LocalizationService localization)
    {
        _localization = localization;
    }

    public void Show(string message) => MessageQueue.Enqueue(message);

    /// <summary>Message with a button (e.g. "Prikaži dokumente"). Stays a bit longer, so there is time to click it.</summary>
    public void Show(string message, string actionText, Action action)
        => MessageQueue.Enqueue<object?>(message, actionText, _ => action(), null,
                                         false, false, TimeSpan.FromSeconds(8));

    public void ShowLocalized(string key) => MessageQueue.Enqueue(_localization.Get(key));

    public void ShowError(Exception ex)
    {
        Log.Warning("Error shown to the user", ex);


        string key = ex is ApiException apiEx ? $"Error_{apiEx.Kind}" : "Error_Unexpected";
        MessageQueue.Enqueue(_localization.Get(key));
    }
}