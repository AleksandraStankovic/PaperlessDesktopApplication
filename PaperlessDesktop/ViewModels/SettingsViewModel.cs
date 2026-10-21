using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly LocalizationService _localization;

    public SettingsViewModel(LocalizationService localization)
    {
        _localization = localization;
    }

    [RelayCommand]
    private void SetLanguage(string code) => _localization.SetLanguage(code);
}