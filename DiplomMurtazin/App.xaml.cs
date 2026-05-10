using DiplomMurtazin.Core;
using PdfSharp.Fonts;
using System.Windows;

namespace DiplomMurtazin
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Users CurrentUser { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            GlobalFontSettings.FontResolver = new FontResolver();

            if (!ConnectionManager.TestServerConnection(out var error))
            {
                MessageBox.Show("Не удалось подключиться к серверной базе данных.\n" +
                               "Проверьте параметр ServerSqlConnection в App.config.\n\n" +
                               $"Техническая причина: {error}",
                               "Критическая ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);

                Current.Shutdown();
                return;
            }

            if (!DatabaseManager.EnsureDatabaseReady(out var migrationError))
            {
                MessageBox.Show("Не удалось подготовить серверную базу данных.\n" +
                               $"Техническая причина: {migrationError}",
                               "Критическая ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);

                Current.Shutdown();
            }
        }
    }
}