using System.Windows;
using PaperlessDesktop.ViewModels;

namespace PaperlessDesktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}