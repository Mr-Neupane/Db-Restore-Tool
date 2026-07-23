namespace DbRestoreTool.Model;

public class PsqlSettings
{
    public string Host { get; set; } = "localhost";
    public string Port { get; set; } = "5432";
    public string Database { get; set; } = "postgres";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; }
    public string ZipPassword { get; set; }
}