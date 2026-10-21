using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels
{
    public partial class DocumentsViewModel : ObservableObject
    {
        private readonly PaperlessApiClient _api;
        private readonly NotificationService _notifications;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private int? _documentCount;


        public DocumentsViewModel(PaperlessApiClient api, NotificationService notifications)
        {
            _api = api;
            _notifications = notifications;
        }


        [RelayCommand]
        private async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                // Temporary: just get the total count. Phase 3 loads the real list.
                var result = await _api.GetDocumentsAsync(new DocumentQuery { PageSize = 1 });
                DocumentCount = result.Count;
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

    }
}
