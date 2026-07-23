using DbRestoreTool.Validator.Interface;

namespace DbRestoreTool.Validator;

public class ArchiveValidator : IArchiveValidator
{
    public bool IsArchive(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext == ".7z" || ext == ".zip" || ext == ".rar";
    }
}