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
    /// <summary>
    /// Interaction logic for TagEditorView.xaml
    /// </summary>
    public partial class TagEditorView : UserControl
    {
        public TagEditorView()
        {
            InitializeComponent();
        }

        // The name box is focused when the dialog opens, which would normally paint its border and
        // label in the accent color before the user has done anything. The XAML gives it neutral
        // colors instead; as soon as the user types or leaves the box, the local values are cleared
        // and the box goes back to the normal style (accent while focused, like every other text box).
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
