using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const int SaveDelayMilliseconds = 400;

    // Quick choices for the accent color (Material Design palette)
    private static readonly string[] PresetColorValues =
    {
        "#2563EB", "#3F51B5", "#00897B", "#388E3C", "#F57C00",
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

    /// <summary>Shown on the page, so it's clear the settings really are stored locally.</summary>
    public string SettingsFilePath => _settingsService.FilePath;

    // 0 = Srpski, 1 = English
    [ObservableProperty]
    private int _languageIndex;

    // 0 = light, 1 = dark
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDarkTheme))]
    private int _themeIndex;

    public bool IsDarkTheme => ThemeIndex == 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryColorHex))]
    private Color _primaryColor;

    public string PrimaryColorHex => ThemeService.ToHex(PrimaryColor);

    /// <summary>"✓ Saved at 16:52:10" - visible confirmation (H1).</summary>
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

        // Show the loaded settings without re-applying or re-saving them
        _initializing = true;
        LanguageIndex = settings.Language == "en" ? 1 : 0;
        ThemeIndex = settings.IsDarkTheme ? 1 : 0;
        PrimaryColor = ThemeService.ParseColor(settings.PrimaryColor, ThemeService.DefaultPrimary);
        _initializing = false;
    }

    // ---------- Commands ----------

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

    // ---------- Every change: apply immediately, then save ----------

    partial void OnLanguageIndexChanged(int oldValue, int newValue)
    {
        if (newValue < 0)
        {
            LanguageIndex = oldValue;   // clicking the selected option again must not "unselect" it
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

    // ---------- Helpers ----------

    private void ApplyTheme() => _theme.Apply(IsDarkTheme, PrimaryColor);

    /// <summary>
    /// Dragging in the color picker changes the color many times per second.
    /// We only write the file once the user pauses (same idea as the search delay).
    /// </summary>
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
            return;   // another change came in - a newer save is scheduled
        }

        _settings.Language = LanguageIndex == 1 ? "en" : "sr";
        _settings.IsDarkTheme = IsDarkTheme;
        _settings.PrimaryColor = PrimaryColorHex;

        if (_settingsService.Save(_settings))
            SavedText = string.Format(_localization.Get("Settings_SavedAt"), DateTime.Now.ToString("HH:mm:ss"));
        else
            _notifications.ShowLocalized("Settings_SaveFailed");
    }
}