using DiplomMurtazin.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace DiplomMurtazin.View
{
    public partial class ReturnProcessingPage : Page
    {
        private readonly ReturnProcessingViewModel _viewModel;

        public ReturnProcessingPage()
        {
            InitializeComponent();
            _viewModel = new ReturnProcessingViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.LoadSoldUnits();
        }
    }
}
