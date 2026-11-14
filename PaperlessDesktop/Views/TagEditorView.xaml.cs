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

namespace PaperlessDesktop.Views
{
    public partial class TagEditorView : UserControl
    {
        public TagEditorView()
        {
            InitializeComponent();
        }

        private void OnNameBoxLoaded(object sender, RoutedEventArgs e)
        {
            NameBox.Loaded -= OnNameBoxLoaded;
            NameBox.TextChanged += OnNameBoxInteracted;
            NameBox.LostKeyboardFocus += OnNameBoxInteracted;
        }

        private void OnNameBoxInteracted(object sender, RoutedEventArgs e)
        {
            NameBox.TextChanged -= OnNameBoxInteracted;
            NameBox.LostKeyboardFocus -= OnNameBoxInteracted;
            NameBox.ClearValue(MaterialDesignThemes.Wpf.TextFieldAssist.UnderlineBrushProperty);
            NameBox.ClearValue(MaterialDesignThemes.Wpf.TextFieldAssist.OutlinedBorderActiveThicknessProperty);
            NameBox.ClearValue(MaterialDesignThemes.Wpf.HintAssist.ForegroundProperty);
        }
    }
}
