using System.Text;
using Genarate_OldVsNew_Filepaths.Models;

namespace Genarate_OldVsNew_Filepaths.Services;

public class PathBuilderService
{
    public string BuildPathFromSegments(
        string targetRootFolder,
        IReadOnlyList<(string value, string trailingDelimiter)> segments,
        string baseToFirstDelimiter,
        string fileName,
        string? extension = null,
        string extensionDelimiter = ".")
    {
        string normalizedBase = targetRootFolder.TrimEnd('\\', '/');
        var sb = new StringBuilder(normalizedBase);
        string lastDelimiter = baseToFirstDelimiter;

        foreach (var (value, trailingDelimiter) in segments)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                sb.Append(lastDelimiter);
                sb.Append(value);
                lastDelimiter = trailingDelimiter;
            }
        }

        string fileWithExt = BuildFileNameWithExtension(fileName, extension, extensionDelimiter);
        if (!string.IsNullOrWhiteSpace(fileWithExt))
        {
            sb.Append(lastDelimiter);
            sb.Append(fileWithExt);
        }

        return sb.ToString();
    }

    public string BuildNewPath(string basePath, string itemFolder, string revision, string fileName, string? extension = null, PathDelimiters? delimiters = null)
    {
        delimiters ??= new PathDelimiters();
        var segments = new List<(string value, string trailingDelimiter)>();
        if (!string.IsNullOrWhiteSpace(itemFolder))
            segments.Add((itemFolder, delimiters.ItemToRevision));
        if (!string.IsNullOrWhiteSpace(revision))
            segments.Add((revision, delimiters.RevisionToFile));

        return BuildPathFromSegments(basePath, segments, delimiters.BaseToItem, fileName, extension, delimiters.FileToExtension);
    }

    public string BuildOutputLine(string oldPath, string newPath, string delimiter)
    {
        return $"{oldPath}{delimiter}{newPath}";
    }

    private string BuildFileNameWithExtension(string fileName, string? extension, string extDelimiter)
    {
        if (string.IsNullOrWhiteSpace(extension)) return fileName;

        string trimmedExt = extension.Trim();
        if (!string.IsNullOrEmpty(extDelimiter) && trimmedExt.StartsWith(extDelimiter))
            return $"{fileName}{trimmedExt}";
        if (extDelimiter == "." && trimmedExt.StartsWith('.'))
            return $"{fileName}{trimmedExt}";

        return $"{fileName}{extDelimiter}{trimmedExt}";
    }
}
