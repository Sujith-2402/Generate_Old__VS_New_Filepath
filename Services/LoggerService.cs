using System.Text;
using Genarate_OldVsNew_Filepaths.Models;

namespace Genarate_OldVsNew_Filepaths.Services;

public class LoggerService
{
    private readonly object _lock = new();
    public string CurrentLogFilePath { get; private set; } = string.Empty;

    public void Initialize(string inputFilePath)
    {
        string inputDir = Path.GetDirectoryName(inputFilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string logsDir = Path.Combine(inputDir, "Logs");
        Directory.CreateDirectory(logsDir);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        CurrentLogFilePath = Path.Combine(logsDir, $"Log_{timestamp}.txt");
    }

    public void LogHeader(string inputFile, AppConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("  PROCESS LOG: Genarate_OldVsNew_Filepaths");
        sb.AppendLine($"  Start Time : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Input File : {inputFile}");
        sb.AppendLine($"  Output File: {config.OutputFileName}");
        sb.AppendLine($"  Custom Base: {config.CustomBasePath}");
        sb.AppendLine($"  Delimiter  : {config.Delimiter}");
        sb.AppendLine($"  DataSource : {config.DataSourceFilter ?? "None (All Rows)"}");
        sb.AppendLine("================================================================================");
        AppendRaw(sb.ToString());
    }

    public void LogInfo(string message) => WriteEntry("INFO", message);
    public void LogWarning(string message) => WriteEntry("WARN", message);

    public void LogError(string message, Exception? ex = null)
    {
        string detail = ex == null ? message : $"{message} | Details: {ex.Message}";
        WriteEntry("ERROR", detail);
    }

    public void LogSummary(int total, int written, int skipped, TimeSpan duration, string status)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($"  EXECUTION SUMMARY: {status}");
        sb.AppendLine($"  Total Input Rows: {total} | Output Written: {written} | Skipped: {skipped}");
        sb.AppendLine($"  Elapsed Time    : {duration.TotalSeconds:F2}s ({duration})");
        sb.AppendLine($"  End Time        : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("================================================================================");
        AppendRaw(sb.ToString());
    }

    private void WriteEntry(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";
        AppendRaw(line + Environment.NewLine);
    }

    private void AppendRaw(string text)
    {
        if (string.IsNullOrEmpty(CurrentLogFilePath)) return;
        lock (_lock) { File.AppendAllText(CurrentLogFilePath, text, Encoding.UTF8); }
    }
}
