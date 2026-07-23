namespace DbRestoreTool.Model;

public class MsSqlSettings
{
    public string ServerName { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string OutputDirectory { get; set; }
    public List<string> ZipPassword { get; set; }
    public string DataLocation { get; set; }
    public string AppPath { get; set; }
    public bool DBnameWithDate { get; set; } = true;
    public int BufferCount { get; set; } = 1024;
    public int MaxTransferSize { get; set; } = 4194304;
}