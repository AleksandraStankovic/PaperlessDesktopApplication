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

        // Below this width of the list+preview area the preview covers the list (full width)
        // instead of sitting next to it; side by side, both would be too narrow to be useful.
        private const double PreviewOverlayMaxWidth = 760;

        // Side-by-side minimums so the splitter can't squeeze either pane into a strip.
        private const double ListMinWidth = 280;
        private const double PreviewMinWidth = 320;

        // The file preview keeps at least this share of the card height; metadata above it scrolls.
        private const double PreviewMetaMaxShare = 0.35;
        private const double PreviewMetaMinHeight = 90;

        // Below this list width the "Datum dokumenta" and "Dodato" columns are hidden so the title fits.
        private const double ListDatesMinWidth = 720;
        private bool? _listShowsDates;

        // Below this list width "Korespondent" and "Tip dokumenta" are hidden too, instead of
        // being squeezed into empty slivers; title and tags remain.
        private const double ListMetaMinWidth = 560;
        private bool? _listShowsMeta;

        // List/preview split; equal by default, remembered after the user drags the splitter.
        private GridLength _listWidth = new(1, GridUnitType.Star);
        private GridLength _previewWidth = new(1, GridUnitType.Star);

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

        private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateContentColumns();
        }

        private void OnPreviewVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateContentColumns();
        }

        private void OnSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _listWidth = ListColumn.Width;
            _previewWidth = PreviewColumn.Width;
        }

        private void OnPreviewCardSizeChanged(object sender, SizeChangedEventArgs e)
        {
            PreviewMetaScroll.MaxHeight = Math.Max(PreviewMetaMinHeight, e.NewSize.Height * PreviewMetaMaxShare);
        }

        private void OnDocumentsGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Column order (see DocumentsView.xaml): 0 title, 1 document date, 2 date added,
            // 3 correspondent, 4 document type, 5 tags.
            bool showDates = e.NewSize.Width >= ListDatesMinWidth;
            if (_listShowsDates != showDates)
            {
                _listShowsDates = showDates;
                var visibility = showDates ? Visibility.Visible : Visibility.Collapsed;
                DocumentsGrid.Columns[1].Visibility = visibility;
                DocumentsGrid.Columns[2].Visibility = visibility;
            }

            bool showMeta = e.NewSize.Width >= ListMetaMinWidth;
            if (_listShowsMeta != showMeta)
            {
                _listShowsMeta = showMeta;
                var visibility = showMeta ? Visibility.Visible : Visibility.Collapsed;
                DocumentsGrid.Columns[3].Visibility = visibility;
                DocumentsGrid.Columns[4].Visibility = visibility;
            }
        }

        private void UpdateContentColumns()
        {
            // Visibility (not IsVisible) so this doesn't react to the whole page being hidden.
            bool open = PreviewCard.Visibility == Visibility.Visible;
            bool overlay = ContentGrid.ActualWidth > 0 && ContentGrid.ActualWidth < PreviewOverlayMaxWidth;

            if (!open)
            {
                SetColumns(new GridLength(1, GridUnitType.Star), 0, new GridLength(0), 0, splitter: false);
            }
            else if (overlay)
            {
                // Preview spans the whole area on top of the list; its close button brings the list back.
                SetColumns(new GridLength(1, GridUnitType.Star), 0, new GridLength(0), 0, splitter: false);
                Place(PreviewCard, 0, 0, 3, new Thickness(0));
                return;
            }
            else
            {
                SetColumns(_listWidth, ListMinWidth, _previewWidth, PreviewMinWidth, splitter: true);
            }

            Place(PreviewCard, 0, 2, 1, new Thickness(0));
        }

        private void SetColumns(GridLength list, double listMin, GridLength preview, double previewMin, bool splitter)
        {
            ListColumn.Width = list;
            ListColumn.MinWidth = listMin;
            PreviewColumn.Width = preview;
            PreviewColumn.MinWidth = previewMin;
            SplitterColumn.Width = splitter ? GridLength.Auto : new GridLength(0);
            PreviewSplitter.Visibility = splitter ? Visibility.Visible : Visibility.Collapsed;
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
