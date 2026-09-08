using System.Data;
using System.Diagnostics;
using System.Text;
using Genarate_OldVsNew_Filepaths.Models;

namespace Genarate_OldVsNew_Filepaths.Services;

public class Genarate_OldVsNew_Filepaths
{
    private readonly ExcelReaderService _excelReader = new();
    private readonly PathBuilderService _pathBuilder = new();

    public async Task<int> ProcessAsync(
        string inputExcelPath, AppConfig config, LoggerService logger,
        IProgress<string> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        logger.LogHeader(inputExcelPath, config);
        string outPath = GetOutputPath(inputExcelPath, config.OutputFileName);
        var table = _excelReader.ReadExcelToDataTable(inputExcelPath, config.ExcelSheetName);
        ValidateColumnsExist(table, config);
        logger.LogInfo($"Loaded {table.Rows.Count} rows. Filter: '{config.DataSourceFilter ?? "All"}'. Output: {outPath}");

        var (written, skipped) = await WriteOutputFileAsync(table, config, outPath, progress, ct);
        logger.LogSummary(table.Rows.Count, written, skipped, sw.Elapsed, "SUCCESS");
        return written;
    }

    private string GetOutputPath(string inputExcelPath, string fileName)
    {
        string dir = Path.GetDirectoryName(inputExcelPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(dir, fileName);
    }

    private async Task<(int written, int skipped)> WriteOutputFileAsync(
        DataTable table, AppConfig config, string outPath,
        IProgress<string> progress, CancellationToken ct)
    {
        using var writer = new StreamWriter(outPath, false, Encoding.UTF8);
        int written = 0, skipped = 0;
        for (int i = 0; i < table.Rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string? line = ProcessRow(table.Rows[i], config);
            if (line != null) { await writer.WriteLineAsync(line.AsMemory(), ct); written++; }
            else { skipped++; }
            if ((i + 1) % 50 == 0 || i == table.Rows.Count - 1)
                progress.Report($"Processed {i + 1} of {table.Rows.Count} records...");
        }
        return (written, skipped);
    }

    private string? ProcessRow(DataRow row, AppConfig config)
    {
        if (!MatchesDataSource(row, config)) return null;

        string oldPath = GetColVal(row, config.Columns.OldPathColumn);
        string item = GetColVal(row, config.Columns.ItemFolderColumn);
        string rev = GetColVal(row, config.Columns.RevisionColumn);
        string fileName = GetColVal(row, config.Columns.FileNameColumn);
        string ext = GetColVal(row, config.Columns.ExtensionColumn);
        if (string.IsNullOrWhiteSpace(oldPath) && string.IsNullOrWhiteSpace(item)) return null;

        string newPath = _pathBuilder.BuildNewPath(config.CustomBasePath, item, rev, fileName, ext);
        return _pathBuilder.BuildOutputLine(oldPath, newPath, config.Delimiter);
    }

    private bool MatchesDataSource(DataRow row, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.DataSourceFilter)) return true;
        string colName = config.Columns.DataSourceColumn ?? "DataSource";
        string actualVal = GetColVal(row, colName);
        return string.Equals(actualVal, config.DataSourceFilter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private string GetColVal(DataRow row, string colName) =>
        row.Table.Columns.Contains(colName) ? row[colName]?.ToString()?.Trim() ?? string.Empty : string.Empty;

    private void ValidateColumnsExist(DataTable table, AppConfig config)
    {
        var cols = config.Columns;
        string[] required = [cols.OldPathColumn, cols.ItemFolderColumn, cols.RevisionColumn, cols.FileNameColumn, cols.ExtensionColumn];
        foreach (var col in required)
        {
            if (!table.Columns.Contains(col))
                throw new InvalidOperationException($"Required column '{col}' not found in input file.");
        }
        if (!string.IsNullOrWhiteSpace(config.DataSourceFilter) && !table.Columns.Contains(cols.DataSourceColumn ?? "DataSource"))
            throw new InvalidOperationException($"DataSource column '{cols.DataSourceColumn}' not found for filtering.");
    }
}
