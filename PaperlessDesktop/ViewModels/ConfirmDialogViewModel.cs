namespace PaperlessDesktop.ViewModels;

public class ConfirmDialogViewModel
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }

    public bool IsDestructive { get; }

    public ConfirmDialogViewModel(string title, string message, string confirmText, bool isDestructive)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        IsDestructive = isDestructive;
    }
}