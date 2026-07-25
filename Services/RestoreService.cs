using System.Diagnostics;
using DbRestoreTool.Helpers.Interfaces;
using DbRestoreTool.Model;
using DbRestoreTool.Services.Interfaces;
using DbRestoreTool.Validator.Interface;
using Microsoft.Extensions.Options;

namespace DbRestoreTool.Services
{
    public class RestoreService : IRestoreService
    {
        private readonly IFileExtractionHelper _extractionHelper;
        private readonly MsSqlSettings _msSqlSettings;
        private readonly IRestoreHelper _restoreHelper;
        private readonly IArchiveValidator _archiveValidator;
        private readonly IClipboardCopyHelper _copyHelper;
        private readonly IRawQueryService _rawQueries;
        private readonly RestoreSetting _setting;


        public RestoreService(IFileExtractionHelper extractionHelper, IOptions<MsSqlSettings> config,
            IRestoreHelper restoreHelper, IArchiveValidator archiveValidator,
            IClipboardCopyHelper copyHelper,
            IRawQueryService rawQueries, IOptions<RestoreSetting> setting)
        {
            _extractionHelper = extractionHelper;
            _msSqlSettings = config.Value;
            _restoreHelper = restoreHelper;
            _archiveValidator = archiveValidator;
            _copyHelper = copyHelper;
            _rawQueries = rawQueries;
            _setting = setting.Value;
        }

        public async Task ValidateAndRestore(string inputPath)
        {
            if (!_setting.IsPostgres && !_setting.SelectionForServer)
            {
                await PerformMsSqlRestore(inputPath);
            }
            else if (_setting.IsPostgres && _setting.SelectionForServer)
            {
                Console.WriteLine("Both values from RestoreSetting can not true");
            }
            else
            {
                if (_setting.SelectionForServer)
                {
                    Console.WriteLine("Press Y if restore type is Postgres, if not press any key:");
                    var input = Console.ReadLine();
                    _setting.IsPostgres = input?.Trim().ToUpper() == "Y";
                }

                if (_setting.IsPostgres)
                {
                    await PerformPsqlRestore(inputPath);
                }
                else
                {
                    await PerformMsSqlRestore(inputPath, true);
                }
            }

            var isArchive = _archiveValidator.IsArchive(inputPath);
            if (isArchive)
            {
                _extractionHelper.RemoveExtractedFile();
            }
        }

        async Task PerformMsSqlRestore(string inputFilePath, bool isExplicitName = false)
        {
            var targetDatabaseName = "";
            if (_setting.DbRenameOption)
            {
                Console.WriteLine("Add New DbName");
                targetDatabaseName = Console.ReadLine() ?? "";
            }

            var isArchive = _archiveValidator.IsArchive(inputFilePath);
            var dbPath = !isArchive ? inputFilePath : await _extractionHelper.ExtractArchive(inputFilePath);

            targetDatabaseName = string.IsNullOrEmpty(targetDatabaseName)
                ? await _restoreHelper.GetDatabaseNameFromHeaderAsync(dbPath)
                : targetDatabaseName;

            var fileSize = new FileInfo(inputFilePath).Length;
            Console.WriteLine($"File size: {fileSize / 1024.0 / 1024.0:F2} MB");

            var skipInitialCheck = isArchive && !_msSqlSettings.DBnameWithDate && !isExplicitName;

            var bakFilePath = dbPath;
            if (isArchive)
            {
                if (skipInitialCheck)
                {
                    if (string.IsNullOrEmpty(targetDatabaseName))
                    {
                        try
                        {
                            targetDatabaseName = await _restoreHelper.GetDatabaseNameFromHeaderAsync(bakFilePath);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($"WARNING: Could not read header. Using fallback name: {targetDatabaseName}");
                        }
                    }
                }
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                Console.WriteLine("Restore in progress...");
                await _restoreHelper.RestoreDatabaseAsync(targetDatabaseName, bakFilePath);

                stopwatch.Stop();
                Console.WriteLine("SUCCESS: Restore completed successfully.");


                double dbSizeMb = await _restoreHelper.GetDatabaseSizeInMbAsync(targetDatabaseName);
                string dbSizeStr = dbSizeMb >= 1024 ? $"{dbSizeMb / 1024:F2} GB" : $"{dbSizeMb:F2} MB";
                Console.WriteLine($"{targetDatabaseName}, total size of db (ldf+mdf) in {dbSizeStr}");

                _copyHelper.CopyToClipboard(targetDatabaseName);
                await _rawQueries.GetAndExecuteRawQuery(targetDatabaseName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Error during restore: {ex.Message}");
                throw;
            }
        }


        async Task PerformPsqlRestore(string inputPath)
        {
            try
            {
                var isArchive = _archiveValidator.IsArchive(inputPath);
                var path = isArchive ? await _extractionHelper.ExtractArchive(inputPath) : inputPath;
                var dbName = await _restoreHelper.GetDatabaseNameFromHeaderAsync(path);

                if (_setting.DbRenameOption)
                {
                    Console.WriteLine("Add New Db Name:");
                    dbName = Console.ReadLine()?.Trim() ?? dbName;
                }

                await _restoreHelper.DropAndCreateDatabaseAsync(dbName);
                var stopwatch = Stopwatch.StartNew();
                await _restoreHelper.RestoreDatabaseAsync(dbName, path);
                stopwatch.Stop();
                var timeTaken = stopwatch.Elapsed;

                if (timeTaken.TotalMinutes < 1)
                {
                    Console.WriteLine(
                        $"Restore completed in {Math.Round(timeTaken.TotalSeconds, 2)} second(s)");
                }
                else
                {
                    Console.WriteLine($"Restore completed in {Math.Round(timeTaken.TotalMinutes, 2)} minute(s)");
                }

                await _rawQueries.GetAndExecuteRawQuery(dbName);
                _copyHelper.CopyToClipboard(dbName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}