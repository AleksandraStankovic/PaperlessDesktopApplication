using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels
{
    public partial class TagsViewModel : ObservableObject, INavigationAware
    {
        private readonly PaperlessApiClient _api;
        private readonly DialogService _dialogs;
        private readonly NotificationService _notifications;
        private readonly LocalizationService _localization;
        private bool _loadedOnce;

        public ObservableCollection<Tag> Tags { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
        private bool _isLoading;

        public bool ShowEmptyState => !IsLoading && Tags.Count == 0;

        public event EventHandler? TagsChanged;

        public event EventHandler<Tag>? ShowDocumentsRequested;

        public TagsViewModel(PaperlessApiClient api, DialogService dialogs,
                             NotificationService notifications, LocalizationService localization)
        {
            _api = api;
            _dialogs = dialogs;
            _notifications = notifications;
            _localization = localization;
        }

        public void OnNavigatedTo()
        {
            if (!_loadedOnce)
                _ = LoadAsync();
        }

        public void Invalidate() => _loadedOnce = false;

        [RelayCommand]
        private async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var tags = await _api.GetAllTagsAsync();

                Tags.Clear();
                foreach (var tag in tags.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase))
                    Tags.Add(tag);

                _loadedOnce = true;
            }
            catch (Exception ex)
            {
                _notifications.ShowError(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ShowDocuments(Tag? tag)
        {
            if (tag is not null)
                ShowDocumentsRequested?.Invoke(this, tag);
        }

        [RelayCommand]
        private async Task CreateTagAsync()
        {
            var editor = new TagEditorViewModel(_api, _localization, Tags, tagToEdit: null);
            if (await _dialogs.ShowAsync(editor) is not Tag saved)
                return;

            _notifications.ShowSuccess(string.Format(_localization.Get("Tags_Created"), saved.Name));
            await AfterChangeAsync();
        }

        [RelayCommand]
        private async Task EditTagAsync(Tag? tag)
        {
            if (tag is null)
                return;

            var editor = new TagEditorViewModel(_api, _localization, Tags, tag);
            if (await _dialogs.ShowAsync(editor) is not Tag saved)
                return;

            _notifications.ShowSuccess(string.Format(_localization.Get("Tags_Updated"), saved.Name));
            await AfterChangeAsync();
        }

        [RelayCommand]
        private async Task DeleteTagAsync(Tag? tag)
        {
            if (tag is null)
                return;

            string message = tag.DocumentCount > 0
                ? string.Format(_localization.Get("Tags_DeleteConfirmUsed"), tag.Name, tag.DocumentCount)
                : string.Format(_localization.Get("Tags_DeleteConfirm"), tag.Name);

            bool confirmed = await _dialogs.ConfirmAsync(
                _localization.Get("Tags_DeleteTitle"), message,
                _localization.Get("Common_Delete"), isDestructive: true);
            if (!confirmed)
                return;

            try
            {
                IsLoading = true;
                await _api.DeleteTagAsync(tag.Id);
                _notifications.ShowSuccess(string.Format(_localization.Get("Tags_Deleted"), tag.Name));
                await AfterChangeAsync();
            }
            catch (Exception ex)
            {
                _notifications.ShowError(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AfterChangeAsync()
        {
            await LoadAsync();
            TagsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
