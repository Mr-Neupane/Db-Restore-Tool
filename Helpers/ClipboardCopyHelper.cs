using System.Diagnostics;
using DbRestoreTool.Helpers.Interfaces;

namespace DbRestoreTool.Helpers;

public class ClipboardCopyHelper : IClipboardCopyHelper
{
    public void CopyToClipboard(string text)
    {
        string safeText = text.Replace("'", "''");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-command \"Set-Clipboard -Value '{safeText}'\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            process.WaitForExit();
        }

        Console.WriteLine("");
        Console.WriteLine($"SUCCESS: [Database name '{text}' copied to clipboard]");
    }
}