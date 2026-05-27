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
        private void Reason_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReturnProcessingViewModel vm)
            {
                vm.IsDefective = false;

                vm.ReturnReason =
                    ((RadioButton)sender).Content.ToString();
            }
        }

        private void Defective_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReturnProcessingViewModel vm)
            {
                vm.IsDefective = true;

                vm.ReturnReason =
                    ((RadioButton)sender).Content.ToString();
            }
        }
    }
}
