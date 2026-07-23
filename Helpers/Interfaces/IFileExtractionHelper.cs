namespace DbRestoreTool.Helpers.Interfaces;

public interface IFileExtractionHelper
{
    Task<string> ExtractArchive(string archivePath);
    void RemoveExtractedFile();
}