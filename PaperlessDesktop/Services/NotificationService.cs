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

    public void ShowLocalized(string key) => MessageQueue.Enqueue(_localization.Get(key));

    public void ShowError(Exception ex)
    {
        Log.Warning("Error shown to the user", ex);


        string key = ex is ApiException apiEx ? $"Error_{apiEx.Kind}" : "Error_Unexpected";
        MessageQueue.Enqueue(_localization.Get(key));
    }
}