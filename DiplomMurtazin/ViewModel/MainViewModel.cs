using DiplomMurtazin.Core;
using DiplomMurtazin.View;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        private bool _isSalesSelected;
        private bool _isProductsSelected;
        private Users _currentUser;
        private Frame _mainFrame;
        private bool _isSalesVisible;
        public bool IsSalesVisible { get => _isSalesVisible; set => Set(ref _isSalesVisible, value); }

        private bool _isProductsVisible;
        public bool IsProductsVisible { get => _isProductsVisible; set => Set(ref _isProductsVisible, value); }

        private bool _isTorg12Visible;
        public bool IsTorg12Visible { get => _isTorg12Visible; set => Set(ref _isTorg12Visible, value); }

        private bool _isProductUnitHistoryVisible;
        public bool IsProductUnitHistoryVisible { get => _isProductUnitHistoryVisible; set => Set(ref _isProductUnitHistoryVisible, value); }

        private bool _isHistoryVisible;
        public bool IsHistoryVisible { get => _isHistoryVisible; set => Set(ref _isHistoryVisible, value); }

        private bool _isDashboardVisible;
        public bool IsDashboardVisible { get => _isDashboardVisible; set => Set(ref _isDashboardVisible, value); }

        private bool _isReportsVisible;
        public bool IsReportsVisible { get => _isReportsVisible; set => Set(ref _isReportsVisible, value); }

        private bool _isReturnProcessingVisible;
        public bool IsReturnProcessingVisible { get => _isReturnProcessingVisible; set => Set(ref _isReturnProcessingVisible, value); }

        private bool _isDefectiveVisible;
        public bool IsDefectiveVisible { get => _isDefectiveVisible; set => Set(ref _isDefectiveVisible, value); }

        private bool _isEmployeesVisible;
        public bool IsEmployeesVisible { get => _isEmployeesVisible; set => Set(ref _isEmployeesVisible, value); }

        private bool _isUniversalVisible;
        public bool IsUniversalVisible { get => _isUniversalVisible; set => Set(ref _isUniversalVisible, value); }

        private bool _isViewSettingsVisible;
        public bool IsViewSettingsVisible { get => _isViewSettingsVisible; set => Set(ref _isViewSettingsVisible, value); }

        private bool _isViewDisplayVisible;
        public bool IsViewDisplayVisible
        {
            get => _isViewDisplayVisible; set => Set(ref _isViewDisplayVisible, value);
        }
        private void ApplyRolePermissions()
        {
            // Сбросить видимость всех пунктов (по умолчанию скрыты)
            IsSalesVisible = IsProductsVisible = IsTorg12Visible = IsProductUnitHistoryVisible =
            IsHistoryVisible = IsDashboardVisible = IsReportsVisible = IsReturnProcessingVisible =
            IsDefectiveVisible = IsEmployeesVisible = IsUniversalVisible = IsViewSettingsVisible =
            IsViewDisplayVisible = false;

            string role = CurrentUser?.Role ?? "";

            // Кассир: продажи + оформление возврата
            if (role == "Кассир")
            {
                IsSalesVisible = true;
                IsReturnProcessingVisible = true;
                DefaultPage = "Sales";
            }
            // Кладовщик: товары, ТОРГ-12, история индивидуального товара
            else if (role == "Кладовщик")
            {
                IsProductsVisible = true;
                IsTorg12Visible = true;
                IsProductUnitHistoryVisible = true;
                DefaultPage = "Products";
            }
            // Бухгалтер: история всех товаров, история индивидуального товара, дашборд, отчёты, дефектные товары, представления, просмотр представлений
            else if (role == "Бухгалтер")
            {
                IsHistoryVisible = true;
                IsProductUnitHistoryVisible = true;
                IsDashboardVisible = true;
                IsReportsVisible = true;
                IsDefectiveVisible = true;
                IsViewSettingsVisible = true;
                IsViewDisplayVisible = true;
                DefaultPage = "Dashbord";
            }
            // Администратор: видит всё
            else if (role == "Администратор" || role == "Admin")
            {
                IsSalesVisible = true;
                IsProductsVisible = true;
                IsTorg12Visible = true;
                IsProductUnitHistoryVisible = true;
                IsHistoryVisible = true;
                IsDashboardVisible = true;
                IsReportsVisible = true;
                IsReturnProcessingVisible = true;
                IsDefectiveVisible = true;
                IsEmployeesVisible = true;
                IsUniversalVisible = true;
                IsViewSettingsVisible = true;
                IsViewDisplayVisible = true;
                DefaultPage = "Torg12"; // Или оставить как было
            }
            else // неизвестная роль – минимальные права
            {
                IsSalesVisible = true;
                DefaultPage = "Sales";
            }
        }

        private string DefaultPage = "Sales";
        public bool IsSalesSelected
        {
            get => _isSalesSelected;
            set => Set(ref _isSalesSelected, value);
        }

        public bool IsProductsSelected
        {
            get => _isProductsSelected;
            set => Set(ref _isProductsSelected, value);
        }

        public Users CurrentUser
        {
            get => _currentUser;
            set => Set(ref _currentUser, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand LogoutCommand { get; }

        public MainViewModel(Frame mainFrame)
        {
            _mainFrame = mainFrame;
            CurrentUser = App.CurrentUser;
            NavigateCommand = new RelayCommand(Navigate);
            LogoutCommand = new RelayCommand(Logout);

            ApplyRolePermissions();          // установить видимость кнопок
            NavigateCommand = new RelayCommand(Navigate);
            LogoutCommand = new RelayCommand(Logout);

            // Переход на страницу по умолчанию для роли
            if (!string.IsNullOrEmpty(DefaultPage))
                Navigate(DefaultPage);
            else
                Navigate("Sales");
        }
        private bool HasAccessToPage(string pageName)
        {
            string role = CurrentUser?.Role ?? "";
            switch (pageName)
            {
                case "Sales": return role == "Кассир" || role == "Администратор" || role == "Admin";
                case "ReturnProcessing": return role == "Кассир" || role == "Администратор" || role == "Admin";
                case "Products": return role == "Кладовщик" || role == "Администратор" || role == "Admin";
                case "Torg12": return role == "Кладовщик" || role == "Администратор" || role == "Admin";
                case "ProductUnitHistory": return role == "Кладовщик" || role == "Бухгалтер" || role == "Администратор" || role == "Admin";
                case "History": return role == "Бухгалтер" || role == "Администратор" || role == "Admin";
                case "Dashbord": return role == "Бухгалтер" || role == "Администратор" || role == "Admin";
                case "Reports": return role == "Бухгалтер" || role == "Администратор" || role == "Admin";
                case "Defective Products": return role == "Бухгалтер" || role == "Администратор" || role == "Admin";
                case "ViewSettings": return role == "Бухгалтер" || role == "Администратор" || role == "Admin";
                case "ViewDisplay": return role == "Бухгалтер" || role == "Администратор" || role == "Admin";
                case "Employees": return role == "Администратор" || role == "Admin";
                case "Universal": return role == "Администратор" || role == "Admin";
                default: return false;
            }
        }
        private void Navigate(object parameter)
        {
            string pageName = parameter as string;  
            if (string.IsNullOrEmpty(pageName)) return;

            // Проверка прав доступа для запрашиваемой страницы
            if (!HasAccessToPage(pageName))
            {
                MessageBox.Show("У вас нет прав доступа к этому разделу.", "Доступ запрещён",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            switch (pageName)
            {
                case "Sales":
                    _mainFrame.Navigate(new SalesPage());
                    IsSalesSelected = true;
                    IsProductsSelected = false;
                    break;

                case "Products":
                    _mainFrame.Navigate(new ProductsPage());
                    IsSalesSelected = false;
                    IsProductsSelected = true;
                    break;

                case "Universal":
                    _mainFrame.Navigate(new UniversalEditPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;

                case "Categories":
                    // _mainFrame.Navigate(new CategoriesPage());
                    break;

                case "Employees":
                    _mainFrame.Navigate(new EmployeesPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;

                case "Reports":
                    _mainFrame.Navigate(new ReportsPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;

                case "Torg12":
                    _mainFrame.Navigate(new Torg12Page());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;

                case "History":
                    _mainFrame.Navigate(new HistoryPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;

                case "ViewSettings":
                    _mainFrame.Navigate(new ViewSettingsPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;
                case "ViewDisplay":
                    _mainFrame.Navigate(new ViewDisplayPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;
                case "Notifications":
                    _mainFrame.Navigate(new NotificationsPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;
                case "ReturnProcessing":
                    _mainFrame.Navigate(new ReturnProcessingPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;
                case "ProductUnitHistory":
                    _mainFrame.Navigate(new ProductUnitHistoryPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;

                   case  "Dashbord":
                    _mainFrame.Navigate(new DashboardPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;
                case "Defective Products":
                    _mainFrame.Navigate(new DefectiveProductsPage());
                    IsSalesSelected = false;
                    IsProductsSelected = false;
                    break;

            }
        }

        private void Logout(object parameter)
        {
            var result = MessageBox.Show("Вы действительно хотите выйти?",
                                        "Подтверждение",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                App.CurrentUser = null;

                var authWindow = new AuthorizationWindow();
                authWindow.Show();

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow)
                    {
                        window.Close();
                        break;
                    }
                }
            }
        }
    }
}