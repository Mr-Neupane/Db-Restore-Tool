using System.Diagnostics;
using DbRestoreTool.Model;

namespace DbRestoreTool
{
    public static class EnvSetup
    {
        public static void Setup(MsSqlSettings setting)
        {
            if (string.IsNullOrWhiteSpace(setting.AppPath))
            {
                Console.WriteLine("ERROR: AppPath is not configured in config.json.");
                return;
            }

            try
            {
                Console.WriteLine("\n*** Setting up Environment ***");


                if (Path.GetPathRoot(setting.AppPath).TrimEnd('\\') == setting.AppPath.TrimEnd('\\'))
                {
                    Console.WriteLine(
                        $"ERROR: Cannot install to root directory '{setting.AppPath}'. Please specify a subdirectory.");
                    return;
                }

                if (!Directory.Exists(setting.AppPath))
                {
                    Directory.CreateDirectory(setting.AppPath);
                    Console.WriteLine($"SUCCESS: Created directory: {setting.AppPath}");
                }

                string currentDir = AppDomain.CurrentDomain.BaseDirectory;


                if (string.Equals(Path.GetFullPath(currentDir).TrimEnd('\\'),
                        Path.GetFullPath(setting.AppPath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        "WARNING: You are running the tool from the target AppPath. Skipping publish/copy.");
                }
                else
                {
                    string projectDir = FindProjectDirectory(currentDir);
                    if (string.IsNullOrEmpty(projectDir))
                    {
                        Console.WriteLine(
                            "WARNING: Could not find .csproj file. Attempting to copy current executable...");
                        try
                        {
                            string currentExe = Environment.ProcessPath;
                            if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
                            {
                                currentExe = Path.Combine(currentDir, "DbRestoreTool.exe");
                            }

                            if (File.Exists(currentExe))
                            {
                                string targetExe = Path.Combine(setting.AppPath, Path.GetFileName(currentExe));
                                File.Copy(currentExe, targetExe, true);
                                Console.WriteLine($"SUCCESS: Successfully copied executable to {setting.AppPath}");
                            }
                            else
                            {
                                Console.WriteLine("ERROR: Could not locate executable to copy.");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR: Failed to copy executable: {ex.Message}");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Found project at: {projectDir}");
                        Console.WriteLine("Publishing application to AppPath...");

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"publish \"{projectDir}\" -o \"{setting.AppPath}\" -c Release",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            process.WaitForExit();

                            if (process.ExitCode == 0)
                            {
                                Console.WriteLine($"SUCCESS: Successfully published to {setting.AppPath}");
                            }
                            else
                            {
                                string error = process.StandardError.ReadToEnd();
                                Console.WriteLine($"ERROR: Publish failed: {error}");
                                return;
                            }
                        }
                    }


                    string currentConfig = Path.Combine(currentDir, "appsettings.json");
                    string targetConfig = Path.Combine(setting.AppPath, "appsettings.json");

                    if (File.Exists(currentConfig))
                    {
                        try
                        {
                            File.Copy(currentConfig, targetConfig, true);
                            Console.WriteLine("SUCCESS: Configuration file copied to AppPath.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR: Failed to copy appsettings.json: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("WARNING: No appsettings.json found in current directory to copy.");
                    }
                }


                string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                if (pathEnv != null && !pathEnv.Contains(setting.AppPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure we don't have a double semicolon
                    string newPath = pathEnv.EndsWith(";") ? pathEnv + setting.AppPath : pathEnv + ";" + setting.AppPath;

                    Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                    Console.WriteLine($"SUCCESS: Added {setting.AppPath} to User PATH.");
                    Console.WriteLine("You may need to restart your terminal for changes to take effect.");
                }
                else
                {
                    Console.WriteLine($"{setting.AppPath} is already in PATH.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Environment setup failed: {ex.Message}");
            }
        }


        private static string FindProjectDirectory(string startPath)
        {
            DirectoryInfo dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (dir.GetFiles("*.csproj").Length > 0)
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return null;
        }
    }
}