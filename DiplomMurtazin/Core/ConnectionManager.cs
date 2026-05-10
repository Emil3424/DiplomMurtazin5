using System;
using System.Configuration;
using System.Data.EntityClient;
using System.Data.SqlClient;

namespace DiplomMurtazin.Core
{
    public static class ConnectionManager
    {
        private const string DefaultDatabaseName = "KPMurtazin";

        public static string GetServerConnectionString()
        {
            var configured = ConfigurationManager.AppSettings["ServerSqlConnection"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return @"Data Source=.\SQLEXPRESS;Initial Catalog=KPMurtazin;Integrated Security=True;Connect Timeout=30;MultipleActiveResultSets=True";
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