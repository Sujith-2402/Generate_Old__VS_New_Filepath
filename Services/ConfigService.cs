using System.Text.Json;
using Genarate_OldVsNew_Filepaths.Models;

namespace Genarate_OldVsNew_Filepaths.Services;

public class ConfigService
{
    public AppConfig LoadConfig(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: {filePath}");

        string json = File.ReadAllText(filePath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, GetJsonOptions())
            ?? throw new InvalidOperationException("Failed to deserialize configuration.");

        ValidateConfig(config);
        return config;
    }

    private JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    private void ValidateConfig(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.OutputFileName))
            throw new ArgumentException("OutputFileName must be specified in config.");

        if (string.IsNullOrEmpty(config.Delimiter))
            throw new ArgumentException("Delimiter must be specified in config.");

        if (string.IsNullOrWhiteSpace(config.CustomBasePath))
            throw new ArgumentException("CustomBasePath must be specified in config.");

        ValidateColumns(config.Columns);
    }

    private void ValidateColumns(ColumnMapping cols)
    {
        if (string.IsNullOrWhiteSpace(cols.OldPathColumn))
            throw new ArgumentException("Columns.OldPathColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.ItemFolderColumn))
            throw new ArgumentException("Columns.ItemFolderColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.RevisionColumn))
            throw new ArgumentException("Columns.RevisionColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.FileNameColumn))
            throw new ArgumentException("Columns.FileNameColumn is required.");
        if (string.IsNullOrWhiteSpace(cols.ExtensionColumn))
            throw new ArgumentException("Columns.ExtensionColumn is required.");
    }
}
