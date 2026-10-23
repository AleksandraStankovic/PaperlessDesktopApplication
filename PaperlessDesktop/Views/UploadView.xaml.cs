using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using PaperlessDesktop.ViewModels;

namespace PaperlessDesktop.Views
{
    /// <summary>
    /// Interaction logic for UploadView.xaml
    /// </summary>
    public partial class UploadView : UserControl
    {
        public UploadView()
        {
            InitializeComponent();
        }


        private UploadViewModel? ViewModel => DataContext as UploadViewModel;

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = ViewModel.FileDialogFilter,   // only supported types are offered
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
                ViewModel.SelectFile(dialog.FileName);
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            bool accepted = false;

            if (ViewModel is { IsBusy: false }
                && e.Data.GetData(DataFormats.FileDrop) is string[] files
                && files.Length > 0)
            {
                accepted = UploadViewModel.IsSupportedFile(files[0]);
                ViewModel.SetDragState(isOver: true, isRejected: !accepted);
            }

            // The mouse cursor itself shows "can drop" / "can't drop"
            e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
            => ViewModel?.SetDragState(isOver: false, isRejected: false);

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            ViewModel?.SetDragState(isOver: false, isRejected: false);

            if (ViewModel is null || e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
                return;

            ViewModel.SelectFile(files[0], multipleDropped: files.Length > 1);
        }


    }
}
