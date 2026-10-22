using MaterialDesignThemes.Wpf;
using PaperlessDesktop.ViewModels;

namespace PaperlessDesktop.Services;

/// <summary>Shows dialogs on top of the main window, so ViewModels don't need to touch UI controls.</summary>
public class DialogService
{
    /// <summary>Must match Identifier="RootDialog" on the DialogHost in MainWindow.xaml.</summary>
    public const string HostId = "RootDialog";

    /// <summary>Shows a dialog whose content is a ViewModel (its View comes from a DataTemplate). Returns the dialog's result.</summary>
    public async Task<object?> ShowAsync(object content)
        => await DialogHost.Show(content, HostId);

    /// <summary>Yes/no question. Returns true only if the user confirmed.</summary>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool isDestructive = false)
    {
        var dialog = new ConfirmDialogViewModel(title, message, confirmText, isDestructive);
        var result = await DialogHost.Show(dialog, HostId);
        return result is true;
    }

    /// <summary>Closes the open dialog and hands back a result to whoever opened it.</summary>
    public static void Close(object? result)
    {
        if (DialogHost.IsDialogOpen(HostId))
            DialogHost.Close(HostId, result);
    }
}