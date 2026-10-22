namespace PaperlessDesktop.ViewModels;

/// <summary>Content of a yes/no dialog.</summary>
public class ConfirmDialogViewModel
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }

    /// <summary>True for dangerous actions (delete) - the confirm button is shown in red.</summary>
    public bool IsDestructive { get; }

    public ConfirmDialogViewModel(string title, string message, string confirmText, bool isDestructive)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        IsDestructive = isDestructive;
    }
}