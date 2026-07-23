namespace DbRestoreTool.Services.Interfaces;

public interface IRestoreService
{
    Task ValidateAndRestore(string inputPath);
}