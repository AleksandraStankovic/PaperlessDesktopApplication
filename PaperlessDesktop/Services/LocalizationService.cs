using System.Windows;

namespace PaperlessDesktop.Services;

public class LocalizationService
{
    public static readonly string[] SupportedLanguages = { "sr", "en" };

    public string CurrentLanguage { get; private set; } = "sr";

    public void SetLanguage(string code)
    {
        if (!SupportedLanguages.Contains(code))
            code = "en";

        var newDictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Languages/Strings.{code}.xaml")
        };

        var merged = Application.Current.Resources.MergedDictionaries;

        // Remove the currently loaded language file (whichever it is)
        var old = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Languages/Strings.") == true);
        if (old is not null)
            merged.Remove(old);

        merged.Add(newDictionary);
        CurrentLanguage = code;
    }

    public string Get(string key)
        => Application.Current.TryFindResource(key) as string ?? key;
}