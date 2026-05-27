using DiplomMurtazin.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace DiplomMurtazin.View
{
    public partial class ProductUnitHistoryPage : Page
    {
        private readonly ProductUnitHistoryViewModel _viewModel;

        public ProductUnitHistoryPage()
        {
            InitializeComponent();
            _viewModel = new ProductUnitHistoryViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }
}
