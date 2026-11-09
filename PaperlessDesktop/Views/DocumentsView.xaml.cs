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
    /// Interaction logic for DocumentsView.xaml
    /// </summary>
    public partial class DocumentsView : UserControl
    {
        // Below this width the search box gets its own line and the 3 filter dropdowns share the next one.
        // (search ~300 + 3 dropdowns ~160 + gaps; below that the dropdown hints start to get cut off)
        private const double FilterSingleRowMinWidth = 860;

        // Below this width the view toggle + refresh button move under the page title.
        private const double HeaderSingleRowMinWidth = 640;

        private bool? _filtersWide;
        private bool? _headerWide;

        public DocumentsView()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout(e.NewSize.Width);
        }

        private void ApplyResponsiveLayout(double width)
        {
            bool filtersWide = width >= FilterSingleRowMinWidth;
            if (_filtersWide != filtersWide)
            {
                _filtersWide = filtersWide;

                if (filtersWide)
                {
                    // [ search (wider) ][ tag ][ correspondent ][ type ]
                    SearchColumn.Width = new GridLength(1.6, GridUnitType.Star);
                    Place(SearchPanel, 0, 0, 1, new Thickness(0, 0, 16, 8));
                    Place(TagFilter, 0, 1, 1, new Thickness(0, 0, 12, 8));
                    Place(CorrespondentFilter, 0, 2, 1, new Thickness(0, 0, 12, 8));
                    Place(DocumentTypeFilter, 0, 3, 1, new Thickness(0, 0, 0, 8));
                }
                else
                {
                    // [ search ............................ ]
                    // [ tag ][ correspondent ][ type ]
                    // Column 0 collapses; search spans the 3 filter columns.
                    SearchColumn.Width = new GridLength(0);
                    Place(SearchPanel, 0, 1, 3, new Thickness(0, 0, 0, 12));
                    Place(TagFilter, 1, 1, 1, new Thickness(0, 0, 12, 8));
                    Place(CorrespondentFilter, 1, 2, 1, new Thickness(0, 0, 12, 8));
                    Place(DocumentTypeFilter, 1, 3, 1, new Thickness(0, 0, 0, 8));
                }
            }

            bool headerWide = width >= HeaderSingleRowMinWidth;
            if (_headerWide != headerWide)
            {
                _headerWide = headerWide;

                if (headerWide)
                    Place(HeaderActions, 0, 1, 1, new Thickness(16, 0, 0, 0));
                else
                    Place(HeaderActions, 1, 0, 2, new Thickness(0, 12, 0, 0));

                HeaderActions.HorizontalAlignment = headerWide ? HorizontalAlignment.Right : HorizontalAlignment.Left;
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
