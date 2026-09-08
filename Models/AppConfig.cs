namespace Genarate_OldVsNew_Filepaths.Models;

public class AppConfig
{
    public string OutputFileName { get; set; } = "OldVsNew_Filepaths_Output.txt";
    public string Delimiter { get; set; } = "|";
    public string CustomBasePath { get; set; } = string.Empty;
    public string? ExcelSheetName { get; set; }
    public string? DataSourceFilter { get; set; }
    public ColumnMapping Columns { get; set; } = new ColumnMapping();
}

public class ColumnMapping
{
    public string OldPathColumn { get; set; } = "OldPath";
    public string ItemFolderColumn { get; set; } = "ItemName";
    public string RevisionColumn { get; set; } = "Revision";
    public string FileNameColumn { get; set; } = "FileName";
    public string ExtensionColumn { get; set; } = "Extension";
    public string? DataSourceColumn { get; set; }
}
