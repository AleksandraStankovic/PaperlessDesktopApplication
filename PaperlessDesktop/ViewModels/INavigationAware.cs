namespace PaperlessDesktop.ViewModels;

/// <summary>Pages that want to know when the user opens them (e.g. to load data the first time).</summary>
public interface INavigationAware
{
    void OnNavigatedTo();
}