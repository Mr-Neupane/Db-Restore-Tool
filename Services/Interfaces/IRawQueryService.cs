namespace DbRestoreTool.Services.Interfaces;

public interface IRawQueryService
{
    Task GetAndExecuteRawQuery(string dbName);
}