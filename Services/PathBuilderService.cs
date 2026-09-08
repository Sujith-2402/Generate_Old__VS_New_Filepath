namespace Genarate_OldVsNew_Filepaths.Services;

public class PathBuilderService
{
    public string BuildNewPath(string basePath, string itemFolder, string revision, string fileName, string extension)
    {
        string formattedExt = FormatExtension(extension);
        string fileWithExt = $"{fileName}{formattedExt}";
        return CombineCustomPath(basePath, itemFolder, revision, fileWithExt);
    }

    public string BuildOutputLine(string oldPath, string newPath, string delimiter)
    {
        return $"{oldPath}{delimiter}{newPath}";
    }

    private string FormatExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        string trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    private string CombineCustomPath(string basePath, string item, string rev, string fileWithExt)
    {
        string normalizedBase = basePath.TrimEnd('\\', '/');
        return $@"{normalizedBase}\{item}\{rev}\{fileWithExt}";
    }
}
