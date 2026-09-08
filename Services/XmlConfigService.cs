using System.Xml.Linq;
using Genarate_OldVsNew_Filepaths.Models;

namespace Genarate_OldVsNew_Filepaths.Services;

public class XmlConfigService
{
    public AppConfig LoadConfig(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"XML configuration file not found: {filePath}");

        var doc = XDocument.Load(filePath);
        var config = ParseXml(doc);
        ValidateConfig(config);
        return config;
    }

    private AppConfig ParseXml(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("Root <Configuration> element is missing.");
        return new AppConfig
        {
            OutputFileName = GetVal(root, "OutputFileName", "OldVsNew_Filepaths_Output.txt"),
            Delimiter = GetVal(root, "Delimiter", "|"),
            CustomBasePath = GetVal(root, "CustomBasePath", string.Empty),
            ExcelSheetName = GetValOrNull(root, "ExcelSheetName"),
            Columns = ParseColumns(root.Element("Columns"))
        };
    }

    private ColumnMapping ParseColumns(XElement? elem)
    {
        if (elem == null) throw new InvalidOperationException("Missing <Columns> element in XML configuration.");
        return new ColumnMapping
        {
            OldPathColumn = GetVal(elem, "OldPathColumn", "OldPath"),
            ItemFolderColumn = GetVal(elem, "ItemFolderColumn", "ItemName"),
            RevisionColumn = GetVal(elem, "RevisionColumn", "Revision"),
            FileNameColumn = GetVal(elem, "FileNameColumn", "FileName"),
            ExtensionColumn = GetVal(elem, "ExtensionColumn", "Extension")
        };
    }

    private string GetVal(XElement parent, string tag, string def = "") =>
        parent.Element(tag)?.Value?.Trim() ?? def;

    private string? GetValOrNull(XElement parent, string tag)
    {
        string val = GetVal(parent, tag);
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    private void ValidateConfig(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.OutputFileName))
            throw new ArgumentException("OutputFileName must be specified in XML.");
        if (string.IsNullOrEmpty(config.Delimiter))
            throw new ArgumentException("Delimiter must be specified in XML.");
        if (string.IsNullOrWhiteSpace(config.CustomBasePath))
            throw new ArgumentException("CustomBasePath must be specified in XML.");
        ValidateColumns(config.Columns);
    }

    private void ValidateColumns(ColumnMapping cols)
    {
        if (string.IsNullOrWhiteSpace(cols.OldPathColumn)) throw new ArgumentException("OldPathColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.ItemFolderColumn)) throw new ArgumentException("ItemFolderColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.RevisionColumn)) throw new ArgumentException("RevisionColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.FileNameColumn)) throw new ArgumentException("FileNameColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.ExtensionColumn)) throw new ArgumentException("ExtensionColumn is required.");
    }
}
