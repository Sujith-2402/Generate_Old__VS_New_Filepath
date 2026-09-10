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
        var colsElem = root.Element("Columns");

        string basePath = GetVal(colsElem, "TargetRootFolder", "");
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = GetVal(root, "TargetRootFolder", "");
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = GetVal(colsElem, "CustomBasePath", "");
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = GetVal(root, "CustomBasePath", string.Empty);

        var columns = ParseColumns(colsElem, root, out string outputDelim);

        if (string.IsNullOrWhiteSpace(outputDelim))
            outputDelim = GetVal(root, "Delimiter", GetVal(root, "Delimeter", "|"));

        string? updatedInputFileName = GetValOrNull(root, "UpdatedInputFileName") ??
                                       GetValOrNull(root, "OutputUpdatedFileName") ??
                                       GetValOrNull(root, "UpdatedFileName");

        return new AppConfig
        {
            OutputFileName = GetVal(root, "OutputFileName", "OldVsNew_Filepaths_Output.txt"),
            UpdatedInputFileName = updatedInputFileName,
            Delimiter = outputDelim,
            CustomBasePath = basePath,
            ExcelSheetName = GetValOrNull(root, "ExcelSheetName"),
            DataSourceFilter = GetValOrNull(colsElem, "DataSourceFilter") ?? GetValOrNull(root, "DataSourceFilter"),
            Columns = columns
        };
    }

    private ColumnMapping ParseColumns(XElement? elem, XElement root, out string outputDelim)
    {
        outputDelim = "";
        var mapping = new ColumnMapping();

        string oldPath = GetVal(elem, "SourcePathColumn", "");
        if (string.IsNullOrWhiteSpace(oldPath))
            oldPath = GetVal(elem, "OldPathColumn", "OldPath");
        mapping.OldPathColumn = oldPath;

        mapping.ItemFolderColumn = GetVal(elem, "ItemFolderColumn", "ItemName");
        mapping.RevisionColumn = GetVal(elem, "RevisionColumn", "Revision");
        mapping.DataSourceColumn = GetVal(elem, "DataSourceColumn", GetVal(root, "DataSourceColumn", "DataSource"));

        mapping.NewFilePathColumn = GetVal(elem, "NewFilePathColumn", GetVal(elem, "NewFilePathColumnName", GetVal(root, "NewFilePathColumn", "NewFilePath")));
        mapping.InsertAfterColumn = GetValOrNull(elem, "InsertAfterColumn") ??
                                    GetValOrNull(elem, "InsertAfter") ??
                                    GetValOrNull(root, "InsertAfterColumn") ??
                                    GetValOrNull(root, "InsertAfter");

        var withExtElem = elem?.Element("FileNameWithExtension");
        var withoutExtElem = elem?.Element("FileNameWithoutExtension");

        if (withExtElem != null)
        {
            mapping.HasSeparateExtension = false;
            mapping.ExtensionColumn = null;
            string fn = GetVal(withExtElem, "FileNameColumn", GetVal(withExtElem, "FileName", ""));
            mapping.FileNameColumn = !string.IsNullOrWhiteSpace(fn) ? fn : (withExtElem.Value?.Trim() ?? "FileName");
        }
        else if (withoutExtElem != null)
        {
            mapping.HasSeparateExtension = true;
            string fn = GetVal(withoutExtElem, "FileNameColumn", GetVal(withoutExtElem, "FileName", "FileName"));
            mapping.FileNameColumn = fn;
            string ext = GetVal(withoutExtElem, "ExtensionColumn", GetVal(withoutExtElem, "Extension", "Extension"));
            mapping.ExtensionColumn = ext;

            string? extDelim = GetValOrNull(withoutExtElem, "Delimeter") ?? GetValOrNull(withoutExtElem, "Delimiter");
            if (extDelim != null)
                mapping.Delimiters.FileToExtension = extDelim;
        }
        else
        {
            mapping.HasSeparateExtension = true;
            mapping.FileNameColumn = GetVal(elem, "FileNameColumn", "FileName");
            mapping.ExtensionColumn = GetVal(elem, "ExtensionColumn", "Extension");
        }

        if (elem != null)
        {
            string lastTag = "";
            ColumnSegment? currentSegment = null;

            foreach (var child in elem.Elements())
            {
                string tag = child.Name.LocalName;
                if (tag.Equals("Delimeter", StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("Delimiter", StringComparison.OrdinalIgnoreCase))
                {
                    string val = child.Value ?? string.Empty;

                    if (lastTag.Equals("SourcePathColumn", StringComparison.OrdinalIgnoreCase) ||
                        lastTag.Equals("OldPathColumn", StringComparison.OrdinalIgnoreCase))
                    {
                        outputDelim = val;
                    }
                    else if (lastTag.Equals("TargetRootFolder", StringComparison.OrdinalIgnoreCase) ||
                             lastTag.Equals("CustomBasePath", StringComparison.OrdinalIgnoreCase))
                    {
                        mapping.Delimiters.BaseToItem = val;
                    }
                    else if (lastTag.Equals("ItemFolderColumn", StringComparison.OrdinalIgnoreCase) ||
                             lastTag.Equals("ItemFolder", StringComparison.OrdinalIgnoreCase))
                    {
                        mapping.Delimiters.ItemToRevision = val;
                        if (currentSegment != null) currentSegment.Delimiter = val;
                    }
                    else if (lastTag.Equals("RevisionColumn", StringComparison.OrdinalIgnoreCase) ||
                             lastTag.Equals("Revision", StringComparison.OrdinalIgnoreCase))
                    {
                        mapping.Delimiters.RevisionToFile = val;
                        if (currentSegment != null) currentSegment.Delimiter = val;
                    }
                    else if (lastTag.Equals("FileNameColumn", StringComparison.OrdinalIgnoreCase) ||
                             lastTag.Equals("FileName", StringComparison.OrdinalIgnoreCase))
                    {
                        mapping.Delimiters.FileToExtension = val;
                    }
                    else if (currentSegment != null)
                    {
                        currentSegment.Delimiter = val;
                    }
                }
                else
                {
                    lastTag = tag;

                    if (!tag.Equals("SourcePathColumn", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("OldPathColumn", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("TargetRootFolder", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("CustomBasePath", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("FileNameWithExtension", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("FileNameWithoutExtension", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("FileNameColumn", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("ExtensionColumn", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("DataSourceColumn", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("DataSourceFilter", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("NewFilePathColumn", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("NewFilePathColumnName", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("InsertAfterColumn", StringComparison.OrdinalIgnoreCase) &&
                        !tag.Equals("InsertAfter", StringComparison.OrdinalIgnoreCase))
                    {
                        string colVal = child.Value?.Trim() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(colVal))
                        {
                            currentSegment = new ColumnSegment
                            {
                                TagName = tag,
                                ColumnName = colVal,
                                Delimiter = @"\"
                            };
                            mapping.Segments.Add(currentSegment);
                        }
                        else
                        {
                            currentSegment = null;
                        }
                    }
                    else
                    {
                        currentSegment = null;
                    }
                }
            }
        }

        return mapping;
    }

    private string GetVal(XElement? parent, string tag, string def = "") =>
        parent?.Element(tag)?.Value?.Trim() ?? def;

    private string? GetValOrNull(XElement? parent, string tag)
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
            throw new ArgumentException("TargetRootFolder / CustomBasePath must be specified in XML.");
        ValidateColumns(config.Columns);
    }

    private void ValidateColumns(ColumnMapping cols)
    {
        if (string.IsNullOrWhiteSpace(cols.OldPathColumn)) throw new ArgumentException("OldPathColumn/SourcePathColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.ItemFolderColumn)) throw new ArgumentException("ItemFolderColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.RevisionColumn)) throw new ArgumentException("RevisionColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.FileNameColumn)) throw new ArgumentException("FileNameColumn is required.");
        if (cols.HasSeparateExtension && string.IsNullOrWhiteSpace(cols.ExtensionColumn))
            throw new ArgumentException("ExtensionColumn is required when separate extension is configured.");
    }
}
