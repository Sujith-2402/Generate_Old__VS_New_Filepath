using Genarate_OldVsNew_Filepaths.Models;
using Genarate_OldVsNew_Filepaths.Services;
using Xunit;

namespace Genarate_OldVsNew_Filepaths.Tests;

public class ServicesTests
{
    [Fact]
    public void PathBuilder_BuildsExactExpectedPath()
    {
        var service = new PathBuilderService();
        string basePath = @"I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW\";
        string item = "ITEM_1000002";
        string rev = "0";
        string file = "ITEM_1000002";
        string ext = "SLDPRT";

        string newPath = service.BuildNewPath(basePath, item, rev, file, ext);
        string oldPath = @"D:\PLM TeamcenterX\DEV\R1_SolidWorks\Cryo-Machinery\04-4-3111\000002265-001 cartoon.SLDPRT";
        string result = service.BuildOutputLine(oldPath, newPath, "|");

        string expected = @"D:\PLM TeamcenterX\DEV\R1_SolidWorks\Cryo-Machinery\04-4-3111\000002265-001 cartoon.SLDPRT|I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW\ITEM_1000002\0\ITEM_1000002.SLDPRT";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConfigService_LoadsValidJsonConfiguration()
    {
        var configService = new ConfigService();
        string tempJson = Path.Combine(Path.GetTempPath(), "test_config_" + Guid.NewGuid().ToString("N") + ".json");
        string jsonContent = "{\n  \"OutputFileName\": \"OldVsNew_Filepaths_Output.txt\",\n  \"Delimiter\": \"|\",\n  \"CustomBasePath\": \"I:\\\\Data\\\\SW\",\n  \"Columns\": {\n    \"OldPathColumn\": \"OldPath\",\n    \"ItemFolderColumn\": \"ItemName\",\n    \"RevisionColumn\": \"Revision\",\n    \"FileNameColumn\": \"FileName\",\n    \"ExtensionColumn\": \"Extension\"\n  }\n}";
        File.WriteAllText(tempJson, jsonContent);

        try
        {
            var config = configService.LoadConfig(tempJson);
            Assert.Equal("OldVsNew_Filepaths_Output.txt", config.OutputFileName);
            Assert.Equal("|", config.Delimiter);
            Assert.Equal("OldPath", config.Columns.OldPathColumn);
        }
        finally
        {
            if (File.Exists(tempJson)) File.Delete(tempJson);
        }
    }

    [Fact]
    public void XmlConfigService_LoadsSampleXmlConfigFile()
    {
        var xmlConfigService = new XmlConfigService();
        string configPath = Path.GetFullPath(@"..\..\..\..\Genarate_OldVsNew_Filepaths.xml");

        var config = xmlConfigService.LoadConfig(configPath);

        Assert.Equal("OldVsNew_Filepaths_Output.txt", config.OutputFileName);
        Assert.Equal("|", config.Delimiter);
        Assert.Equal(@"I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW", config.CustomBasePath);
        Assert.Equal("OldPath", config.Columns.OldPathColumn);
        Assert.Equal("ItemName", config.Columns.ItemFolderColumn);
    }

    [Fact]
    public void LoggerService_CreatesLogFileInInputDirectory()
    {
        var logger = new LoggerService();
        string tempFile = Path.Combine(Path.GetTempPath(), "test_input.xlsx");
        File.WriteAllText(tempFile, "dummy");

        try
        {
            logger.Initialize(tempFile);
            logger.LogInfo("Test log entry");

            Assert.True(File.Exists(logger.CurrentLogFilePath));
            string content = File.ReadAllText(logger.CurrentLogFilePath);
            Assert.Contains("Test log entry", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(logger.CurrentLogFilePath)) File.Delete(logger.CurrentLogFilePath);
        }
    }

    [Fact]
    public async Task EndToEndProcessing_GeneratesExpectedOutputFileAndLog()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FilepathGenTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "input_sample.csv");
        string csvContent = "OldPath,ItemName,Revision,FileName,Extension\n" +
            "\"D:\\PLM TeamcenterX\\DEV\\R1_SolidWorks\\Cryo-Machinery\\04-4-3111\\000002265-001 cartoon.SLDPRT\",ITEM_1000002,0,ITEM_1000002,SLDPRT\n";
        await File.WriteAllTextAsync(inputFile, csvContent);

        var config = new AppConfig
        {
            OutputFileName = "OldVsNew_Output.txt",
            Delimiter = "|",
            CustomBasePath = @"I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension"
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();
        var progress = new Progress<string>(_ => { });

        int count = await processor.ProcessAsync(inputFile, config, logger, progress, CancellationToken.None);

        Assert.Equal(1, count);

        string expectedOutputFile = Path.Combine(tempDir, "OldVsNew_Output.txt");
        Assert.True(File.Exists(expectedOutputFile));

        string outputContent = (await File.ReadAllTextAsync(expectedOutputFile)).Trim();
        string expectedLine = @"D:\PLM TeamcenterX\DEV\R1_SolidWorks\Cryo-Machinery\04-4-3111\000002265-001 cartoon.SLDPRT|I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW\ITEM_1000002\0\ITEM_1000002.SLDPRT";

        Assert.Equal(expectedLine, outputContent);
        Assert.True(File.Exists(logger.CurrentLogFilePath));

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DataSourceFilter_FiltersRowsCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FilepathFilterTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "input_filter.csv");
        string csv = "OldPath,ItemName,Revision,FileName,Extension,DataSource\n" +
            "D:\\Path1,ITEM1,0,ITEM1,SLDPRT,SW-SolidWorks\n" +
            "D:\\Path2,ITEM2,0,ITEM2,SLDPRT,OT-OpenText\n" +
            "D:\\Path3,ITEM3,0,ITEM3,SLDPRT,SW-SolidWorks\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Filtered_Output.txt",
            Delimiter = "|",
            CustomBasePath = @"I:\Data\SW",
            DataSourceFilter = "SW-SolidWorks",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension",
                DataSourceColumn = "DataSource"
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(2, count);
        string[] outputLines = await File.ReadAllLinesAsync(Path.Combine(tempDir, "Filtered_Output.txt"));
        Assert.Equal(2, outputLines.Length);
        Assert.Contains("ITEM1", outputLines[0]);
        Assert.Contains("ITEM3", outputLines[1]);

        Directory.Delete(tempDir, true);
    }
}
