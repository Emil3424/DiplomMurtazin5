using System;
using System.Configuration;
using System.Data.EntityClient;
using System.Data.SqlClient;

namespace DiplomMurtazin.Core
{
    public static class ConnectionManager
    {
        private const string DefaultDatabaseName = "KPMurtazin";

        // Храним рабочий connection string после успешной проверки
        private static string _workingSqlConnectionString;
        private static readonly object _lock = new object();

        /// <summary>
        /// Возвращает рабочий SQL connection string (с проверкой доступности сервера)
        /// </summary>
        public static string GetServerConnectionString()
        {
            if (_workingSqlConnectionString != null)
                return _workingSqlConnectionString;

            lock (_lock)
            {
                if (_workingSqlConnectionString != null)
                    return _workingSqlConnectionString;

                // Список возможных строк подключения в порядке приоритета
                var candidates = new[]
                {
                    // 1. Из конфигурации (appSettings)
                    ConfigurationManager.AppSettings["ServerSqlConnection"],
                    // 2. Локальный SQL Express
                    @"Data Source=.\SQLEXPRESS;Initial Catalog=KPMurtazin;Integrated Security=True;Connect Timeout=5",
                    // 3. LocalDB (для разработки)
                    @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=KPMurtazin;Integrated Security=True;Connect Timeout=5",
                    // 4. Простой LocalDB без имени экземпляра (иногда работает)
                    @"Data Source=(localdb)\v11.0;Initial Catalog=KPMurtazin;Integrated Security=True;Connect Timeout=5"
                };

                foreach (var cs in candidates)
                {
                    if (string.IsNullOrWhiteSpace(cs))
                        continue;

                    try
                    {
                        using (var conn = new SqlConnection(cs))
                        {
                            conn.Open();
                            _workingSqlConnectionString = cs;
                            return _workingSqlConnectionString;
                        }
                    }
                    catch
                    {
                    }
                }

                // Если ни один не подошёл – возвращаем исходный (хотя он уже нерабочий)
                return ConfigurationManager.AppSettings["ServerSqlConnection"]
                       ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=KPMurtazin;Integrated Security=True";
            }
        }

        public static string GetDatabaseName()
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(GetServerConnectionString());
                return string.IsNullOrWhiteSpace(builder.InitialCatalog)
                    ? DefaultDatabaseName
                    : builder.InitialCatalog;
            }
            catch
            {
                return DefaultDatabaseName;
            }
        }

        public static string BuildEntityConnectionString()
        {
            var provider = new EntityConnectionStringBuilder
            {
                Provider = "System.Data.SqlClient",
                ProviderConnectionString = GetServerConnectionString(),
                Metadata = @"res://*/Model1.csdl|res://*/Model1.ssdl|res://*/Model1.msl"
            };
            return provider.ConnectionString;
        }

        public static bool TestServerConnection(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                using (var connection = new SqlConnection(GetServerConnectionString()))
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static string BuildMasterConnectionString()
        {
            var builder = new SqlConnectionStringBuilder(GetServerConnectionString())
            {
                InitialCatalog = "master"
            };
            return builder.ConnectionString;
        }

        public static bool DatabaseExists()
        {
            var databaseName = GetDatabaseName();
            try
            {
                using (var connection = new SqlConnection(BuildMasterConnectionString()))
                {
                    connection.Open();
                    using (var command = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @db", connection))
                    {
                        command.Parameters.AddWithValue("@db", databaseName);
                        return (int)command.ExecuteScalar() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}