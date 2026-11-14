using MaterialDesignThemes.Wpf;
using PaperlessDesktop.ViewModels;

namespace PaperlessDesktop.Services;

public class DialogService
{
    public const string HostId = "RootDialog";

    public async Task<object?> ShowAsync(object content)
        => await DialogHost.Show(content, HostId);

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool isDestructive = false)
    {
        var dialog = new ConfirmDialogViewModel(title, message, confirmText, isDestructive);
        var result = await DialogHost.Show(dialog, HostId);
        return result is true;
    }

    public static void Close(object? result)
    {
        if (DialogHost.IsDialogOpen(HostId))
            DialogHost.Close(HostId, result);
    }
}