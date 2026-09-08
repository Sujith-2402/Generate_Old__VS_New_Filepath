using System.Data;
using System.Text;
using ExcelDataReader;

namespace Genarate_OldVsNew_Filepaths.Services;

public class ExcelReaderService
{
    static ExcelReaderService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public DataTable ReadExcelToDataTable(string filePath, string? sheetName = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = CreateAppropriateReader(filePath, stream);

        var dataSet = reader.AsDataSet(GetDataSetConfiguration());
        return ExtractTargetTable(dataSet, sheetName);
    }

    private IExcelDataReader CreateAppropriateReader(string filePath, Stream stream)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".csv" 
            ? ExcelReaderFactory.CreateCsvReader(stream) 
            : ExcelReaderFactory.CreateReader(stream);
    }

    private ExcelDataSetConfiguration GetDataSetConfiguration()
    {
        return new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        };
    }

    private DataTable ExtractTargetTable(DataSet dataSet, string? sheetName)
    {
        if (dataSet.Tables.Count == 0)
            throw new InvalidOperationException("The Excel workbook contains no tables/sheets.");

        if (!string.IsNullOrWhiteSpace(sheetName))
        {
            if (dataSet.Tables.Contains(sheetName))
                return dataSet.Tables[sheetName]!;
            throw new ArgumentException($"Sheet '{sheetName}' was not found in workbook.");
        }

        return dataSet.Tables[0];
    }
}
