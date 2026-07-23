using System.Diagnostics;
using DbRestoreTool.Helpers.Interfaces;
using DbRestoreTool.Model;
using DbRestoreTool.Providers.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DbRestoreTool.Helpers
{
    public class RestoreHelper : IRestoreHelper
    {
        private readonly MsSqlSettings _setting;
        private readonly PsqlSettings _psqlSettings;
        private readonly IDbConnectionProvider _dbConnectionProvider;
        private readonly RestoreSetting _restoreSetting;

        public RestoreHelper(IOptions<MsSqlSettings> options, IOptions<PsqlSettings> psqlSettings,
            IDbConnectionProvider dbConnectionProvider, IOptions<RestoreSetting> restoreSetting)
        {
            _dbConnectionProvider = dbConnectionProvider;
            _restoreSetting = restoreSetting.Value;
            _psqlSettings = psqlSettings.Value;
            _setting = options.Value;
        }

        public async Task DropAndCreateDatabaseAsync(string dbName)
        {
            if (_restoreSetting.IsPostgres)
            {
                await using var conn = await _dbConnectionProvider.GetPSqlConnectionAsync();
                var drop = $"DROP DATABASE if exists  \"{dbName}\" WITH (FORCE)";
                var create = $"create DATABASE \"{dbName}\";";
                var cmd = conn.CreateCommand();
                cmd.CommandText = drop;
                await cmd.ExecuteNonQueryAsync();
                cmd.CommandText = create;
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                Console.WriteLine("Existing Connections found.");
                Console.WriteLine("Removing existing connections...");
                Console.WriteLine();
                await using var conn = await _dbConnectionProvider.GetMssqlConnectionAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    IF DB_ID('{dbName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{dbName}];
                    END";
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"Restoring database after dropping existing database {dbName}");
            }
        }


        public async Task RestoreDatabaseAsync(string dbName, string backupFilePath, Action<string>? progressCallback = null)
        {
            Console.WriteLine($"Restoring database {dbName}");
            if (_restoreSetting.IsPostgres)
            {
                using var process = new Process();
                bool isSqlFile = backupFilePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);

                process.StartInfo.FileName = !isSqlFile ? "pg_restore" : "psql";
                process.StartInfo.Arguments = !isSqlFile
                    ? $"-U postgres -d {dbName} -j 4 \"{backupFilePath}\""
                    : $"-U postgres -d {dbName} -f \"{backupFilePath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.StartInfo.EnvironmentVariables["PGPASSWORD"] = _psqlSettings.Password;

                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();
                int exitCode = process.ExitCode;
                Console.WriteLine();
                if (exitCode == 0)
                {
                    Console.WriteLine($"{dbName} restored successfully.");
                }
                else
                {
                    Console.WriteLine($"{dbName} restore failed.");
                    Console.WriteLine(error);
                }

                Console.WriteLine();
            }
            else
            {
                await DropAndCreateDatabaseAsync(dbName);
                string logicalDataName = "";
                string logicalLogName = "";

                await using (var conn = await _dbConnectionProvider.GetMssqlConnectionAsync())
                {
                    var fileListSql = $"RESTORE FILELISTONLY FROM DISK = N'{backupFilePath}'";
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = fileListSql;
                    await using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var type = reader["Type"].ToString();
                            var logicalName = reader["LogicalName"].ToString();
                            if (type == "D") logicalDataName = logicalName;
                            else if (type == "L") logicalLogName = logicalName;
                        }
                    }

                    if (string.IsNullOrEmpty(logicalDataName) || string.IsNullOrEmpty(logicalLogName))
                    {
                        throw new Exception("Could not determine logical file names from backup.");
                    }

                    string dataFileName = Path.Combine(_setting.DataLocation, $"{dbName}.mdf");
                    string logFileName = Path.Combine(_setting.DataLocation, $"{dbName}_log.ldf");

                    if (progressCallback != null)
                    {
                        ((SqlConnection)conn).InfoMessage += (_, e) =>
                        {
                            foreach (SqlError error in e.Errors)
                            {
                                progressCallback(error.Message);
                            }
                        };
                    }

                    var restoreSql = $@"
                RESTORE DATABASE [{dbName}] 
                FROM DISK = N'{backupFilePath}' 
                WITH 
                    MOVE N'{logicalDataName}' TO N'{dataFileName}', 
                    MOVE N'{logicalLogName}' TO N'{logFileName}', 
                    NOUNLOAD, 
                    REPLACE, 
                    STATS = 5,
                    BUFFERCOUNT = {_setting.BufferCount},
                    MAXTRANSFERSIZE = {_setting.MaxTransferSize},
                    NO_CHECKSUM";
                    await using var restoreCmd = conn.CreateCommand();
                    restoreCmd.CommandText = restoreSql;
                    restoreCmd.CommandTimeout = 0;
                    await restoreCmd.ExecuteNonQueryAsync();
                }
            }
        }


        public async Task<double> GetDatabaseSizeInMbAsync(string dbName)
        {
            await using var conn = await _dbConnectionProvider.GetMssqlConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"SELECT SUM(size) * 8.0 / 1024.0 FROM sys.master_files WHERE database_id = DB_ID('{dbName}')";
            var res = await cmd.ExecuteScalarAsync();

            if (res != null && res != DBNull.Value)
            {
                return Convert.ToDouble(res);
            }

            return 0;
        }


        public async Task<string> GetDatabaseNameFromHeaderAsync(string backupFilePath)
        {
            if (_restoreSetting.IsPostgres)
            {
                string dbName = "";
                string ext = Path.GetExtension(backupFilePath).ToLower();

                if (ext == ".sql")
                {
                    dbName = Path.GetFileNameWithoutExtension(backupFilePath);
                }
                else
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "pg_restore",
                        Arguments = $"-l \"{backupFilePath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    var process = Process.Start(psi)!;
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    dbName += output
                        .Split('\n')
                        .FirstOrDefault(l => l.Contains("dbname"))
                        ?.Split(':')[1]
                        .Trim();
                }

                return dbName;
            }
            else
            {
                await using var conn = await _dbConnectionProvider.GetMssqlConnectionAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"RESTORE HEADERONLY FROM DISK = N'{backupFilePath}'";
                var read = await cmd.ExecuteReaderAsync();

                if (await read.ReadAsync())
                {
                    var dbName = read["DatabaseName"].ToString();
                    return dbName!;
                }
            }

            throw new Exception("Could not extract database name from backup file header.");
        }
    }
}