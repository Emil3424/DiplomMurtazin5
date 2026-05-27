using DiplomMurtazin.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace DiplomMurtazin.View
{
    public partial class ReturnsWarrantyPage : Page
    {
        private readonly ReturnsWarrantyViewModel _viewModel;

        public ReturnsWarrantyPage()
        {
            InitializeComponent();
            _viewModel = new ReturnsWarrantyViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.LoadSoldUnits();
        }
    }
}
