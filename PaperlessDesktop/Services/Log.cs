using System.IO;
using System.Text;

namespace PaperlessDesktop.Services;

/// <summary>
/// Writes errors and important events to a daily log file in %AppData%\PaperlessDesktop\logs.
/// Logging must NEVER crash the app, so every failure here is swallowed.
/// </summary>
public static class Log
{
    private const int KeepDays = 7;
    private static readonly object Sync = new();

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PaperlessDesktop", "logs");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warning(string message, Exception? ex = null) => Write("WARN", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    /// <summary>Removes log files older than a week, so they don't pile up.</summary>
    public static void DeleteOldFiles()
    {
        try
        {
            if (!Directory.Exists(Folder))
                return;

            foreach (string file in Directory.EnumerateFiles(Folder, "app-*.log"))
            {
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-KeepDays))
                    File.Delete(file);
            }
        }
        catch
        {
            // not important - try again next start
        }
    }

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var text = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(level).Append("] ")
                .AppendLine(message);

            if (ex is not null)
                text.AppendLine(ex.ToString());   // type, message and the full stack trace

            lock (Sync)   // several threads may log at the same time
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, $"app-{DateTime.Now:yyyy-MM-dd}.log"), text.ToString());
            }
        }
        catch
        {
            // logging must never take the app down
        }
    }
}