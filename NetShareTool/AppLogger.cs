using System.Diagnostics;
using System.IO;

namespace NetShareTool;

public static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static string LogFilePath { get; } = CreateLogPath();

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(Exception exception, string message)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            File.AppendAllText(
                LogFilePath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
        }
    }

    private static string CreateLogPath()
    {
        var baseDirectory = AppContext.BaseDirectory;

        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                baseDirectory = Path.GetDirectoryName(exePath) ?? baseDirectory;
            }
        }
        catch
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDirectory, "logs", $"netshare-{DateTime.Now:yyyyMMdd}.log");
    }

    public static void OpenLogFolder()
    {
        var logDirectory = Path.GetDirectoryName(LogFilePath);
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            return;
        }

        Directory.CreateDirectory(logDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = logDirectory,
            UseShellExecute = true
        });
    }
}
