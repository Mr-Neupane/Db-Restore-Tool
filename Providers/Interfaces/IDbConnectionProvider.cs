using System.Data.Common;

namespace DbRestoreTool.Providers.Interfaces;

public interface IDbConnectionProvider
{
    DbConnection GetPSqlConnection(string? database = null);
    DbConnection GetMssqlConnection(string? database = null);
    Task<DbConnection> GetPSqlConnectionAsync(string? database = null);
    Task<DbConnection> GetMssqlConnectionAsync(string? database = null);
}