using System.Data.Common;
using DbRestoreTool.Model;
using DbRestoreTool.Providers.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DbRestoreTool.Providers.Implementations;

public class DbConnectionProvider : IDbConnectionProvider
{
    private readonly MsSqlSettings _setting;
    private readonly PsqlSettings _psqlSettings;

    public DbConnectionProvider(IOptions<MsSqlSettings> setting, IOptions<PsqlSettings> psqlSettings)
    {
        _psqlSettings = psqlSettings.Value;
        _setting = setting.Value;
    }

    public DbConnection GetPSqlConnection(string? database)
    {
        var dbName = database ?? _psqlSettings.Database;
        var connString =
            $"Host={_psqlSettings.Host};Database={dbName};Username={_psqlSettings.Username};Password={_psqlSettings.Password};Port={_psqlSettings.Port}";
        var conn = new NpgsqlConnection(connString);
        conn.Open();
        return conn;
    }

    public DbConnection GetMssqlConnection(string? database)
    {
        var dbName = database ?? "master";
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _setting.ServerName,
            UserID = _setting.Username,
            Password = _setting.Password,
            InitialCatalog = dbName,
            TrustServerCertificate = true,
            Encrypt = false
        };

        var winBuilder = new SqlConnectionStringBuilder
        {
            DataSource = _setting.ServerName,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false
        };

        SqlConnection conn;

        try
        {
            conn = new SqlConnection(builder.ConnectionString);
            conn.Open();
        }
        catch (SqlException)
        {
            conn = new SqlConnection(winBuilder.ConnectionString);
            conn.Open();
        }

        return conn;
    }

    public async Task<DbConnection> GetPSqlConnectionAsync(string? database)
    {
        var dbName = database ?? _psqlSettings.Database;
        var connString =
            $"Host={_psqlSettings.Host};Database={dbName};Username={_psqlSettings.Username};Password={_psqlSettings.Password};Port={_psqlSettings.Port}";
        var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<DbConnection> GetMssqlConnectionAsync(string? database)
    {
        var dbName = database ?? "master";
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _setting.ServerName,
            UserID = _setting.Username,
            Password = _setting.Password,
            InitialCatalog = dbName,
            TrustServerCertificate = true,
            Encrypt = false
        };

        var winBuilder = new SqlConnectionStringBuilder
        {
            DataSource = _setting.ServerName,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false
        };

        try
        {
            var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            return conn;
        }
        catch (SqlException)
        {
            var conn = new SqlConnection(winBuilder.ConnectionString);
            await conn.OpenAsync();
            return conn;
        }
    }
}