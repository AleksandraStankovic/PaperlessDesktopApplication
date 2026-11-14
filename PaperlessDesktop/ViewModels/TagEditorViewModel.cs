using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class TagEditorViewModel : ObservableObject
{
    internal const int MaxNameLength = 128;

    internal static readonly string[] PresetColorValues =
    {
        "#a6cee3", "#1f78b4", "#b2df8a", "#33a02c", "#fb9a99", "#e31a1c",
        "#fdbf6f", "#ff7f00", "#cab2d6", "#6a3d9a", "#b15928", "#000000",
        "#cccccc", "#ffff99", "#8dd3c7", "#17becf", "#80b1d3", "#7570b3",
        "#e7298a", "#fccde5", "#66a61e", "#e6ab02", "#a6761d", "#666666"
    };

    private readonly PaperlessApiClient _api;
    private readonly LocalizationService _localization;
    private readonly Tag? _original;
    private readonly HashSet<string> _otherTagNames;
    private readonly bool _initializing;
    private bool _nameTouched;

    public bool IsNew => _original is null;
    public IReadOnlyList<string> PresetColors => PresetColorValues;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameError))]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    [NotifyPropertyChangedFor(nameof(PreviewName))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    [NotifyPropertyChangedFor(nameof(TextColorHex))]
    private Color _selectedColor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasServerError))]
    private string? _serverError;

    public string ColorHex => $"#{SelectedColor.R:x2}{SelectedColor.G:x2}{SelectedColor.B:x2}";

    public string TextColorHex =>
        0.299 * SelectedColor.R + 0.587 * SelectedColor.G + 0.114 * SelectedColor.B > 150 ? "#000000" : "#ffffff";

    public string PreviewName => string.IsNullOrWhiteSpace(Name) ? "Tag" : Name.Trim();

    public string? NameError => _nameTouched ? Validate() : null;
    public bool HasNameError => NameError is not null;
    public bool HasServerError => ServerError is not null;

    public TagEditorViewModel(PaperlessApiClient api, LocalizationService localization,
                              IEnumerable<Tag> existingTags, Tag? tagToEdit)
    {
        _api = api;
        _localization = localization;
        _original = tagToEdit;

        _otherTagNames = new HashSet<string>(
            existingTags.Where(t => t.Id != tagToEdit?.Id).Select(t => t.Name),
            StringComparer.CurrentCultureIgnoreCase);

        _initializing = true;
        Name = tagToEdit?.Name ?? "";
        SelectedColor = ParseColor(tagToEdit?.Color ?? PresetColorValues[1]);
        _initializing = false;
    }

    [RelayCommand]
    private void SetPresetColor(string? hex)
    {
        if (hex is not null)
            SelectedColor = ParseColor(hex);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        _nameTouched = true;
        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(HasNameError));
        if (Validate() is not null)
            return;

        IsSaving = true;
        ServerError = null;
        try
        {
            string name = Name.Trim();
            Tag saved = IsNew
                ? await _api.CreateTagAsync(name, ColorHex)
                : await _api.UpdateTagAsync(_original!.Id, name, ColorHex);

            DialogService.Close(saved);
        }
        catch (ApiException ex) when (ex.Kind == ApiErrorKind.BadRequest)
        {
            ServerError = ex.TryGetServerMessage() ?? _localization.Get("Tags_Error_Rejected");
        }
        catch (ApiException ex)
        {
            ServerError = _localization.Get($"Error_{ex.Kind}");
        }
        catch (Exception)
        {
            ServerError = _localization.Get("Error_Unexpected");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSave() => !IsSaving && Validate() is null;

    [RelayCommand]
    private void Cancel() => DialogService.Close(null);

    partial void OnNameChanged(string value)
    {
        if (!_initializing)
            _nameTouched = true;
    }

    private string? Validate()
    {
        string name = Name?.Trim() ?? "";

        if (name.Length == 0)
            return _localization.Get("Tags_Error_NameRequired");
        if (name.Length > MaxNameLength)
            return _localization.Get("Tags_Error_NameTooLong");
        if (_otherTagNames.Contains(name))
            return _localization.Get("Tags_Error_NameExists");
        return null;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return Colors.SteelBlue;
        }
    }
}