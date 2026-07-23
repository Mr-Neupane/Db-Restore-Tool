namespace DbRestoreTool.Helpers.Interfaces;

public interface IRestoreHelper
{
    Task DropAndCreateDatabaseAsync(string dbName);
    Task<double> GetDatabaseSizeInMbAsync(string dbName);
    Task<string> GetDatabaseNameFromHeaderAsync(string headerPath);
    Task RestoreDatabaseAsync(string dbName, string backupFilePath, Action<string>? progressCallback = null);
}