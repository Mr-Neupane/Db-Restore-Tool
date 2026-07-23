using System.Diagnostics;
using DbRestoreTool.Helpers.Interfaces;
using DbRestoreTool.Model;
using Microsoft.Extensions.Options;

namespace DbRestoreTool.Helpers;

public class FileExtractionHelper : IFileExtractionHelper
{
    private readonly MsSqlSettings _setting;
    private readonly PsqlSettings _psqlSettings;
    private readonly RestoreSetting _restoreSetting;
    private readonly string _7zPath;

    public FileExtractionHelper(IOptions<MsSqlSettings> setting,
        IOptions<PsqlSettings> psqlSettings, IOptions<RestoreSetting> restoreSetting)
    {
        _restoreSetting = restoreSetting.Value;
        _psqlSettings = psqlSettings.Value;
        _setting = setting.Value;
        _7zPath = Path.Combine(AppContext.BaseDirectory, "zip", "7z.exe");
    }

    public void RemoveExtractedFile()
    {
        Console.WriteLine("Removing extracted file.....");
        var listOfTempDbs = _setting.OutputDirectory;
        if (Directory.Exists(listOfTempDbs))
        {
            try
            {
                var files = Directory.GetFiles(listOfTempDbs);
                foreach (var file in files)
                {
                    File.Delete(file);
                }

                Console.WriteLine("Extracted file successfully deleted.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    public async Task<string> ExtractArchive(string archivePath)
    {
        var extractPath = _setting.OutputDirectory;
        Directory.CreateDirectory(extractPath);

        var passwords = BuildPasswordList(archivePath);

        foreach (var pwd in passwords)
        {
            Console.WriteLine("\nAttempting extraction with 7z...");
            var args = $"x \"{archivePath}\" -aoa -o\"{extractPath}\"";
            if (!string.IsNullOrEmpty(pwd))
                args += $" -p\"{pwd}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = _7zPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo)!;
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("Extraction completed successfully.");
                return FindBackupFile(extractPath);
            }

            Console.WriteLine($"WARNING: Extraction failed (exit code {process.ExitCode}): {error.Trim()}");
        }

        if (Directory.Exists(extractPath))
        {
            try { Directory.Delete(extractPath, true); } catch { }
        }

        throw new Exception("Archive extraction failed with all provided passwords.");
    }

    string FindBackupFile(string extractPath)
    {
        var bakFiles = !_restoreSetting.IsPostgres
            ? Directory.GetFiles(extractPath, "*.bak", SearchOption.TopDirectoryOnly)
            : Directory.GetFiles(extractPath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".backup", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".dump", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var recentBak = bakFiles
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .FirstOrDefault();

        if (recentBak == null)
        {
            throw new FileNotFoundException("No database backup file found after extraction.");
        }

        return recentBak.FullName;
    }

    List<string> BuildPasswordList(string archivePath)
    {
        if (_restoreSetting.IsPostgres)
        {
            return new List<string> { _psqlSettings.ZipPassword ?? "" };
        }

        var passwords = _setting.ZipPassword ?? new List<string>();
        if (passwords.Any(string.IsNullOrEmpty))
        {
            Console.WriteLine("Please specify Zip password");
            Environment.Exit(0);
        }

        return passwords;
    }
}
