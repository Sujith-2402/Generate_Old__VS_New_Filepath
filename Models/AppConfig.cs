namespace Genarate_OldVsNew_Filepaths.Models;

public class AppConfig
{
    public string OutputFileName { get; set; } = "OldVsNew_Filepaths_Output.txt";
    public string Delimiter { get; set; } = "|";
    public string CustomBasePath { get; set; } = string.Empty;
    public string TargetRootFolder
    {
        get => CustomBasePath;
        set => CustomBasePath = value;
    }
    public string? ExcelSheetName { get; set; }
    public string? DataSourceFilter { get; set; }
    public ColumnMapping Columns { get; set; } = new ColumnMapping();
}

public class ColumnSegment
{
    public string TagName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string Delimiter { get; set; } = @"\";
}

public class ColumnMapping
{
    public string OldPathColumn { get; set; } = "OldPath";
    public string SourcePathColumn
    {
        get => OldPathColumn;
        set => OldPathColumn = value;
    }
    public string ItemFolderColumn { get; set; } = "ItemName";
    public string RevisionColumn { get; set; } = "Revision";
    public string FileNameColumn { get; set; } = "FileName";
    public string? ExtensionColumn { get; set; } = "Extension";
    public bool HasSeparateExtension { get; set; } = true;
    public string? DataSourceColumn { get; set; }
    public PathDelimiters Delimiters { get; set; } = new();

    /// <summary>
    /// Generic list of all folder/column segments in document order with their respective delimiters.
    /// </summary>
    public List<ColumnSegment> Segments { get; set; } = new();
}

public class PathDelimiters
{
    public string BaseToItem { get; set; } = @"\";
    public string ItemToRevision { get; set; } = @"\";
    public string RevisionToFile { get; set; } = @"\";
    public string FileToExtension { get; set; } = ".";
}
