using System.Data;
using System.Text;
using ClosedXML.Excel;

namespace Genarate_OldVsNew_Filepaths.Services;

public class TableExportService
{
    public void ExportUpdatedFile(
        DataTable originalTable,
        IReadOnlyDictionary<int, string> rowToNewPathMap,
        string newColumnName,
        string? insertAfterColumn,
        string destinationPath,
        string? sheetName = null)
    {
        string dir = Path.GetDirectoryName(destinationPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var orderedColumns = DetermineColumnOrder(originalTable, newColumnName, insertAfterColumn);
        string ext = Path.GetExtension(destinationPath).ToLowerInvariant();

        if (ext == ".csv")
        {
            ExportToCsv(originalTable, rowToNewPathMap, newColumnName, orderedColumns, destinationPath);
        }
        else
        {
            // Default to XLSX for Excel files (.xlsx, .xls)
            ExportToExcel(originalTable, rowToNewPathMap, newColumnName, orderedColumns, destinationPath, sheetName);
        }
    }

    public List<string> DetermineColumnOrder(DataTable table, string newColumnName, string? insertAfterColumn)
    {
        var cols = table.Columns.Cast<DataColumn>()
            .Select(c => c.ColumnName)
            .Where(c => !c.Equals(newColumnName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrWhiteSpace(insertAfterColumn))
        {
            int idx = cols.FindIndex(c => c.Equals(insertAfterColumn, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                cols.Insert(idx + 1, newColumnName);
                return cols;
            }
        }

        cols.Add(newColumnName);
        return cols;
    }

    private void ExportToCsv(
        DataTable table,
        IReadOnlyDictionary<int, string> rowToNewPathMap,
        string newColumnName,
        List<string> orderedColumns,
        string destinationPath)
    {
        using var writer = new StreamWriter(destinationPath, false, Encoding.UTF8);

        // Header
        string headerLine = string.Join(",", orderedColumns.Select(EscapeCsv));
        writer.WriteLine(headerLine);

        // Rows
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var lineValues = new List<string>(orderedColumns.Count);

            foreach (var col in orderedColumns)
            {
                if (col.Equals(newColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    string pathVal = rowToNewPathMap.TryGetValue(i, out var p) ? p : string.Empty;
                    lineValues.Add(EscapeCsv(pathVal));
                }
                else
                {
                    string cellVal = table.Columns.Contains(col) ? row[col]?.ToString() ?? string.Empty : string.Empty;
                    lineValues.Add(EscapeCsv(cellVal));
                }
            }

            writer.WriteLine(string.Join(",", lineValues));
        }
    }

    private void ExportToExcel(
        DataTable table,
        IReadOnlyDictionary<int, string> rowToNewPathMap,
        string newColumnName,
        List<string> orderedColumns,
        string destinationPath,
        string? sheetName)
    {
        using var workbook = new XLWorkbook();
        string wsName = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName;
        // Clean sheet name of invalid characters if any
        wsName = wsName.Replace("/", "_").Replace("\\", "_").Replace("?", "_").Replace("*", "_").Replace(":", "_").Replace("[", "_").Replace("]", "_");
        if (wsName.Length > 31) wsName = wsName.Substring(0, 31);

        var ws = workbook.Worksheets.Add(wsName);

        // Header Row (Row 1)
        for (int c = 0; c < orderedColumns.Count; c++)
        {
            ws.Cell(1, c + 1).Value = orderedColumns[c];
        }

        // Data Rows (Row 2 .. N+1)
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            int excelRow = i + 2;

            for (int c = 0; c < orderedColumns.Count; c++)
            {
                string col = orderedColumns[c];
                if (col.Equals(newColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    string pathVal = rowToNewPathMap.TryGetValue(i, out var p) ? p : string.Empty;
                    ws.Cell(excelRow, c + 1).Value = pathVal;
                }
                else
                {
                    string cellVal = table.Columns.Contains(col) ? row[col]?.ToString() ?? string.Empty : string.Empty;
                    ws.Cell(excelRow, c + 1).Value = cellVal;
                }
            }
        }

        workbook.SaveAs(destinationPath);
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return string.Empty;
        if (val.Contains('"') || val.Contains(',') || val.Contains('\n') || val.Contains('\r'))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }
}
