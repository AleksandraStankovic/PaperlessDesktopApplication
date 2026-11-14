using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const int SaveDelayMilliseconds = 400;

    private static readonly string[] PresetColorValues =
    {
        "#6D28D9", "#2563EB", "#00897B", "#388E3C", "#FACC15", "#F57C00",
        "#D32F2F", "#C2185B", "#7B1FA2", "#5D4037", "#455A64"
    };

    private readonly LocalizationService _localization;
    private readonly ThemeService _theme;
    private readonly SettingsService _settingsService;
    private readonly UserSettings _settings;
    private readonly NotificationService _notifications;
    private readonly bool _initializing;
    private CancellationTokenSource? _saveCts;

    public IReadOnlyList<string> PresetColors => PresetColorValues;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDarkTheme))]
    private int _themeIndex;

    public bool IsDarkTheme => ThemeIndex == 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryColorHex))]
    private Color _primaryColor;

    public string PrimaryColorHex => ThemeService.ToHex(PrimaryColor);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSavedText))]
    private string? _savedText;

    public bool HasSavedText => SavedText is not null;

    public SettingsViewModel(LocalizationService localization, ThemeService theme,
                             SettingsService settingsService, UserSettings settings,
                             NotificationService notifications)
    {
        _localization = localization;
        _theme = theme;
        _settingsService = settingsService;
        _settings = settings;
        _notifications = notifications;

        _initializing = true;
        LanguageIndex = settings.Language == "en" ? 1 : 0;
        ThemeIndex = settings.IsDarkTheme ? 1 : 0;
        PrimaryColor = ThemeService.ParseColor(settings.PrimaryColor, ThemeService.DefaultPrimary);
        _initializing = false;
    }

    [RelayCommand]
    private void SetPresetColor(string? hex)
    {
        if (hex is not null)
            PrimaryColor = ThemeService.ParseColor(hex, PrimaryColor);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {

        LanguageIndex = 0;
        ThemeIndex = 0;
        PrimaryColor = ThemeService.DefaultPrimary;
    }

    partial void OnLanguageIndexChanged(int oldValue, int newValue)
    {
        if (newValue < 0)
        {
            LanguageIndex = oldValue;
            return;
        }
        if (_initializing)
            return;

        _localization.SetLanguage(newValue == 1 ? "en" : "sr");
        ScheduleSave();
    }

    partial void OnThemeIndexChanged(int oldValue, int newValue)
    {
        if (newValue < 0)
        {
            ThemeIndex = oldValue;
            return;
        }
        if (_initializing)
            return;

        ApplyTheme();
        ScheduleSave();
    }

    partial void OnPrimaryColorChanged(Color value)
    {
        if (_initializing)
            return;

        ApplyTheme();
        ScheduleSave();
    }

    private void ApplyTheme() => _theme.Apply(IsDarkTheme, PrimaryColor);

    private void ScheduleSave()
    {
        _saveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _saveCts = cts;
        _ = SaveAfterDelayAsync(cts.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SaveDelayMilliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _settings.Language = LanguageIndex == 1 ? "en" : "sr";
        _settings.IsDarkTheme = IsDarkTheme;
        _settings.PrimaryColor = PrimaryColorHex;

        if (_settingsService.Save(_settings))
            SavedText = string.Format(_localization.Get("Settings_SavedAt"), DateTime.Now.ToString("HH:mm:ss"));
        else
            _notifications.ShowLocalized("Settings_SaveFailed", NotificationKind.Error);
    }
}