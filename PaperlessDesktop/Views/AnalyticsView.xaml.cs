using System.Windows;
using System.Windows.Controls;

namespace PaperlessDesktop.Views
{
    /// <summary>
    /// Interaction logic for AnalyticsView.xaml
    /// </summary>
    public partial class AnalyticsView : UserControl
    {
        // Below this width the pie chart and the tag cloud don't fit next to each other
        private const double SideBySideMinWidth = 980;

        private bool? _sideBySide;

        public AnalyticsView()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool sideBySide = e.NewSize.Width >= SideBySideMinWidth;
            if (_sideBySide == sideBySide)
                return;
            _sideBySide = sideBySide;

            if (sideBySide)
            {
                Place(TypeCard, 0, 0, 1, new Thickness(0, 0, 8, 16));
                Place(TagCloudCard, 0, 1, 1, new Thickness(8, 0, 0, 16));
            }
            else
            {
                Place(TypeCard, 0, 0, 2, new Thickness(0, 0, 0, 16));
                Place(TagCloudCard, 1, 0, 2, new Thickness(0, 0, 0, 16));
            }
        }

        private static void Place(FrameworkElement element, int row, int column, int columnSpan, Thickness margin)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            Grid.SetColumnSpan(element, columnSpan);
            element.Margin = margin;
        }
    }
}
