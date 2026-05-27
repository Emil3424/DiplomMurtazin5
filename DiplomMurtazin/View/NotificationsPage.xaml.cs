using DiplomMurtazin.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace DiplomMurtazin.View
{
    public partial class NotificationsPage : Page
    {
        private readonly NotificationsViewModel _viewModel;

        public NotificationsPage()
        {
            InitializeComponent();
            _viewModel = new NotificationsViewModel();
            DataContext = _viewModel;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.StartCommand.Execute(null);
        }
    }
}
