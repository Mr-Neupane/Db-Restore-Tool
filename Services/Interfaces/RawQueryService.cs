using DbRestoreTool.Model;
using DbRestoreTool.Providers.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DbRestoreTool.Services.Interfaces;

public class RawQueryService : IRawQueryService
{
    private readonly RestoreSetting _settings;
    private readonly IDbConnectionProvider _dbConnectionProvider;

    public RawQueryService(IOptions<RestoreSetting> settings, IDbConnectionProvider dbConnectionProvider)
    {
        _dbConnectionProvider = dbConnectionProvider;
        _settings = settings.Value;
    }

    public async Task GetAndExecuteRawQuery(string dbName)
    {
        try
        {
            var queryFilePath = _settings.RawQueryFilePath;
            if (File.Exists(queryFilePath))
            {
                Console.WriteLine();
                Console.WriteLine("Executing query file: " + queryFilePath);
                var file = await File.ReadAllTextAsync(queryFilePath);

                if (_settings.IsPostgres)
                {
                    await using var conn = await _dbConnectionProvider.GetPSqlConnectionAsync(dbName);
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = file;
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    await using var conn = await _dbConnectionProvider.GetMssqlConnectionAsync(dbName);
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = file;
                    await cmd.ExecuteNonQueryAsync();
                }

                Console.WriteLine("Successfully executed queries from file.");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"File {queryFilePath} does not exist");
                Console.WriteLine();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}