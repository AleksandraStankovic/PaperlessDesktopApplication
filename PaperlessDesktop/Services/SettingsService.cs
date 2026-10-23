using System.IO;
using System.Text.Json;
using PaperlessDesktop.Models;

namespace PaperlessDesktop.Services;

/// <summary>Loads and saves the user's settings in %AppData%\PaperlessDesktop\settings.json.</summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PaperlessDesktop", "settings.json");

    /// <summary>Reads the settings. A missing or broken file simply gives the defaults - never a crash.</summary>
    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new UserSettings();

            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath), JsonOptions);
            return Sanitize(settings ?? new UserSettings());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UserSettings();
        }
    }

    /// <summary>Writes the settings. Returns false if the file couldn't be written.</summary>
    public bool Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            // Write to a temporary file first, then replace: a crash mid-write never leaves a broken file
            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(tempPath, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Replaces invalid values (e.g. someone edited the file by hand) with defaults.</summary>
    private static UserSettings Sanitize(UserSettings settings)
    {
        if (!LocalizationService.SupportedLanguages.Contains(settings.Language))
            settings.Language = UserSettings.DefaultLanguage;

        if (!ThemeService.IsValidColor(settings.PrimaryColor))
            settings.PrimaryColor = UserSettings.DefaultPrimaryColor;

        return settings;
    }
}