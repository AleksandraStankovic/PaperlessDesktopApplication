using System.IO;
using System.Text.Json;
using PaperlessDesktop.Models;

namespace PaperlessDesktop.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PaperlessDesktop", "settings.json");

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

    public bool Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

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

    private static UserSettings Sanitize(UserSettings settings)
    {
        if (!LocalizationService.SupportedLanguages.Contains(settings.Language))
            settings.Language = UserSettings.DefaultLanguage;

        if (!ThemeService.IsValidColor(settings.PrimaryColor))
            settings.PrimaryColor = UserSettings.DefaultPrimaryColor;

        return settings;
    }
}