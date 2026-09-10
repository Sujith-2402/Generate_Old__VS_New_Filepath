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

        var (success, failed, skipped) = await WriteOutputFileAsync(table, config, outPath, logger, progress, ct);
        string status = failed == 0 ? "SUCCESS" : "COMPLETED WITH ERRORS";
        logger.LogSummary(table.Rows.Count, success, failed, skipped, sw.Elapsed, status);
        return success;
    }

    private string GetOutputPath(string inputExcelPath, string fileName)
    {
        string dir = Path.GetDirectoryName(inputExcelPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(dir, fileName);
    }

    private async Task<(int success, int failed, int skipped)> WriteOutputFileAsync(
        DataTable table, AppConfig config, string outPath, LoggerService logger,
        IProgress<string> progress, CancellationToken ct)
    {
        var allowedDataSources = GetAllowedDataSources(config.DataSourceFilter);
        using var writer = new StreamWriter(outPath, false, Encoding.UTF8);
        int success = 0, failed = 0, skipped = 0;

        for (int i = 0; i < table.Rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = table.Rows[i];
            int rowNumber = i + 2; // 1-based index (Row 1 is header)
            string rowData = FormatRowData(row);

            if (!MatchesDataSource(row, config, allowedDataSources))
            {
                skipped++;
                string dsVal = GetColVal(row, config.Columns.DataSourceColumn ?? "DataSource");
                logger.LogSkipped(rowNumber, $"DataSource '{dsVal}' does not match filter '{config.DataSourceFilter}'", rowData);
                continue;
            }

            var (isValid, line, reason) = ProcessRow(row, config);
            if (isValid && line != null)
            {
                await writer.WriteLineAsync(line.AsMemory(), ct);
                success++;
                logger.LogSuccess(rowNumber, line, rowData);
            }
            else
            {
                failed++;
                logger.LogFailed(rowNumber, reason ?? "Missing or invalid row data", rowData);
            }

            if ((i + 1) % 50 == 0 || i == table.Rows.Count - 1)
                progress.Report($"Processed {i + 1} of {table.Rows.Count} records...");
        }
        return (success, failed, skipped);
    }

    private static string FormatRowData(DataRow row)
    {
        var cols = row.Table.Columns.Cast<DataColumn>()
            .Select(c => $"{c.ColumnName}='{row[c]?.ToString()?.Trim() ?? string.Empty}'");
        return $"[{string.Join(", ", cols)}]";
    }

    private (bool isValid, string? line, string? reason) ProcessRow(DataRow row, AppConfig config)
    {
        var missingCols = new List<string>();

        string oldPath = GetColVal(row, config.Columns.OldPathColumn);
        if (string.IsNullOrWhiteSpace(oldPath))
            missingCols.Add(config.Columns.OldPathColumn);

        string fileName = GetColVal(row, config.Columns.FileNameColumn);
        if (string.IsNullOrWhiteSpace(fileName))
            missingCols.Add(config.Columns.FileNameColumn);

        string? ext = null;
        if (config.Columns.HasSeparateExtension && !string.IsNullOrWhiteSpace(config.Columns.ExtensionColumn))
        {
            ext = GetColVal(row, config.Columns.ExtensionColumn);
            if (string.IsNullOrWhiteSpace(ext))
                missingCols.Add(config.Columns.ExtensionColumn);
        }

        if (config.Columns.Segments.Count > 0)
        {
            foreach (var seg in config.Columns.Segments)
            {
                if (!string.IsNullOrWhiteSpace(seg.ColumnName))
                {
                    string val = GetColVal(row, seg.ColumnName);
                    if (string.IsNullOrWhiteSpace(val))
                        missingCols.Add(seg.ColumnName);
                }
            }
        }
        else
        {
            string item = GetColVal(row, config.Columns.ItemFolderColumn);
            if (string.IsNullOrWhiteSpace(item))
                missingCols.Add(config.Columns.ItemFolderColumn);
            if (!string.IsNullOrWhiteSpace(config.Columns.RevisionColumn))
            {
                string rev = GetColVal(row, config.Columns.RevisionColumn);
                if (string.IsNullOrWhiteSpace(rev))
                    missingCols.Add(config.Columns.RevisionColumn);
            }
        }

        if (missingCols.Count > 0)
        {
            return (false, null, $"Missing or blank value in column(s): {string.Join(", ", missingCols.Distinct())}");
        }

        string newPath;
        if (config.Columns.Segments.Count > 0)
        {
            var segments = config.Columns.Segments
                .Select(s => (value: GetColVal(row, s.ColumnName), trailingDelimiter: s.Delimiter))
                .ToList();

            newPath = _pathBuilder.BuildPathFromSegments(
                config.CustomBasePath,
                segments,
                config.Columns.Delimiters.BaseToItem,
                fileName,
                ext,
                config.Columns.Delimiters.FileToExtension);
        }
        else
        {
            string item = GetColVal(row, config.Columns.ItemFolderColumn);
            string rev = GetColVal(row, config.Columns.RevisionColumn);
            newPath = _pathBuilder.BuildNewPath(config.CustomBasePath, item, rev, fileName, ext, config.Columns.Delimiters);
        }

        string outputLine = _pathBuilder.BuildOutputLine(oldPath, newPath, config.Delimiter);
        return (true, outputLine, null);
    }

    private bool MatchesDataSource(DataRow row, AppConfig config, HashSet<string>? allowedDataSources)
    {
        if (allowedDataSources == null) return true;
        string colName = config.Columns.DataSourceColumn ?? "DataSource";
        string actualVal = GetColVal(row, colName);
        return allowedDataSources.Contains(actualVal);
    }

    private HashSet<string>? GetAllowedDataSources(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;

        var set = filter.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return set.Count > 0 ? set : null;
    }

    private string GetColVal(DataRow row, string colName) =>
        row.Table.Columns.Contains(colName) ? row[colName]?.ToString()?.Trim() ?? string.Empty : string.Empty;

    private void ValidateColumnsExist(DataTable table, AppConfig config)
    {
        var cols = config.Columns;
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cols.OldPathColumn, cols.FileNameColumn };

        if (cols.Segments.Count > 0)
        {
            foreach (var seg in cols.Segments)
            {
                if (!string.IsNullOrWhiteSpace(seg.ColumnName))
                    required.Add(seg.ColumnName);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(cols.ItemFolderColumn)) required.Add(cols.ItemFolderColumn);
            if (!string.IsNullOrWhiteSpace(cols.RevisionColumn)) required.Add(cols.RevisionColumn);
        }

        if (cols.HasSeparateExtension && !string.IsNullOrWhiteSpace(cols.ExtensionColumn))
            required.Add(cols.ExtensionColumn);

        foreach (var col in required)
        {
            if (!table.Columns.Contains(col))
                throw new InvalidOperationException($"Required column '{col}' not found in input file.");
        }
        if (!string.IsNullOrWhiteSpace(config.DataSourceFilter) && !table.Columns.Contains(cols.DataSourceColumn ?? "DataSource"))
            throw new InvalidOperationException($"DataSource column '{cols.DataSourceColumn}' not found for filtering.");
    }
}
