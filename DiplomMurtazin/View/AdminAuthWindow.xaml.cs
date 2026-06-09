using System.Linq;
using System.Windows;

namespace DiplomMurtazin.View
{
    public partial class AdminAuthWindow : Window
    {
        public AdminAuthWindow()
        {
            InitializeComponent();
            LoginTextBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login))
            {
                ShowError("Введите логин администратора");
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите пароль администратора");
                return;
            }

            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    var user = context.Users.FirstOrDefault(u => u.Login == login &&
                                                                 u.Password == password &&
                                                                 u.IsActive == true);
                    if (user != null && (user.Role == "Admin" || user.Role == "Администратор"))
                    {
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        ShowError("Неверный логин/пароль или недостаточно прав");
                    }
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        private void CloseErrorButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorBorder.Visibility = Visibility.Collapsed;
        }
    }
}