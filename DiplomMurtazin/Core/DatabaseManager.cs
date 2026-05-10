using System;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;

namespace DiplomMurtazin.Core
{
    public static class DatabaseManager
    {
        public static bool EnsureDatabaseReady(out string errorMessage)
        {
            errorMessage = null;

            try
            {
                EnsureDatabaseExists();
                ApplyIncrementalMigrations();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static void EnsureDatabaseExists()
        {
            var dbName = ConnectionManager.GetDatabaseName();
            using (var connection = new SqlConnection(ConnectionManager.BuildMasterConnectionString()))
            {
                connection.Open();
                using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @db", connection))
                {
                    checkCmd.Parameters.AddWithValue("@db", dbName);
                    var exists = (int)checkCmd.ExecuteScalar() > 0;
                    if (!exists)
                    {
                        using (var createCmd = new SqlCommand($"CREATE DATABASE [{dbName}]", connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private static void ApplyIncrementalMigrations()
        {
            ExecuteScript(ReadEmbeddedScript("DiplomMurtazin.SQL.ScriptDB.sql"), ConnectionManager.GetServerConnectionString());
            ExecuteScript(ReadEmbeddedScript("DiplomMurtazin.SQL.AuditLogMigration.sql"), ConnectionManager.GetServerConnectionString());
            ExecuteScript(ReadEmbeddedScript("DiplomMurtazin.SQL.Torg12Migration.sql"), ConnectionManager.GetServerConnectionString());
            ExecuteScript(ReadEmbeddedScript("DiplomMurtazin.SQL.ReturnsWarrantyMigration.sql"), ConnectionManager.GetServerConnectionString());
        }

        /// <summary>
        /// Читает встроенный ресурс
        /// </summary>
        private static string ReadEmbeddedScript(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Не найден SQL-ресурс: {resourceName}");
                }

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void ExecuteScript(string script, string connectionString)
        {
            string[] commands = script.Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (string commandText in commands)
                {
                    if (string.IsNullOrWhiteSpace(commandText))
                        continue;

                    try
                    {
                        using (var command = new SqlCommand(commandText, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка выполнения команды: {ex.Message}");
                    }
                }
            }
        }
    }
}