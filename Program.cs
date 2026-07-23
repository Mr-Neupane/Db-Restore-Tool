using DbRestoreTool.Helpers;
using DbRestoreTool.Helpers.Interfaces;
using DbRestoreTool.Model;
using DbRestoreTool.Providers.Implementations;
using DbRestoreTool.Providers.Interfaces;
using DbRestoreTool.Services;
using DbRestoreTool.Services.Interfaces;
using DbRestoreTool.Validator;
using DbRestoreTool.Validator.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings-development.json", optional: true)
    .Build();
var services = new ServiceCollection();

services.Configure<MsSqlSettings>(
        configuration.GetSection("MsSqlConnection"))
    .Configure<RestoreSetting>(configuration.GetSection("RestoreSetting"));

services.Configure<PsqlSettings>(
    configuration.GetSection("PostgresConnection"));


services.AddTransient<IFileExtractionHelper, FileExtractionHelper>()
    .AddTransient<IRestoreHelper, RestoreHelper>()
    .AddTransient<IClipboardCopyHelper, ClipboardCopyHelper>()
    .AddTransient<IRestoreService, RestoreService>()
    .AddTransient<IRawQueryService, RawQueryService>()

    .AddScoped<MsSqlSettings>()
    .AddScoped<RestoreSetting>()
    .AddScoped<PsqlSettings>()
    .AddTransient<IArchiveValidator, ArchiveValidator>()
    .AddTransient<IDbConnectionProvider, DbConnectionProvider>();

var serviceProvider = services.BuildServiceProvider();
string fileName;

if (args.Length == 0)
{
    Console.Write("Enter backup file name (example: data.7z): ");
    fileName = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(fileName))
    {
        Console.WriteLine("File name cannot be empty.");
        return;
    }
}
else
{
    fileName = args[0].Trim();
}

string backupFilePath = Path.IsPathRooted(fileName)
    ? fileName
    : Path.Combine(Directory.GetCurrentDirectory(), fileName);

if (!File.Exists(backupFilePath))
{
    Console.WriteLine($"File not found: {backupFilePath}");
    return;
}

var service = serviceProvider.GetRequiredService<IRestoreService>();
await service.ValidateAndRestore(backupFilePath);