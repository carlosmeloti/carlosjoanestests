using System.Windows;
using AcaiPos.Desktop.ViewModels;

namespace AcaiPos.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void DiscountAuthorizerField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.TryAuthorizeDiscountFromFocus();
    }
}
