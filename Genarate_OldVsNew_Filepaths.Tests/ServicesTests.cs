using System.Data;
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
    public void XmlConfigService_LoadsSampleXmlConfigFile()
    {
        var xmlConfigService = new XmlConfigService();
        string configPath = Path.GetFullPath(@"..\..\..\..\Genarate_OldVsNew_Filepaths.xml");

        var config = xmlConfigService.LoadConfig(configPath);

        Assert.False(string.IsNullOrWhiteSpace(config.OutputFileName));
        Assert.Equal("|", config.Delimiter);
        Assert.Equal(@"I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW", config.CustomBasePath);
        Assert.Equal(@"I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW", config.TargetRootFolder);
        Assert.Equal("OldPath", config.Columns.OldPathColumn);
        Assert.Equal("ItemName", config.Columns.ItemFolderColumn);
        Assert.Equal("DataSource", config.Columns.DataSourceColumn);
        Assert.False(string.IsNullOrWhiteSpace(config.DataSourceFilter));
    }

    [Fact]
    public void XmlConfigService_SupportsRootAndColumnsFilterPlacement()
    {
        var xmlConfigService = new XmlConfigService();
        string tempXml = Path.Combine(Path.GetTempPath(), "legacy_config_" + Guid.NewGuid().ToString("N") + ".xml");
        string legacyContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration>
  <OutputFileName>Output.txt</OutputFileName>
  <Delimiter>|</Delimiter>
  <CustomBasePath>D:\Base</CustomBasePath>
  <DataSourceFilter>SW-SolidWorks, OT-OpenText</DataSourceFilter>
  <Columns>
    <OldPathColumn>OldPath</OldPathColumn>
    <ItemFolderColumn>ItemName</ItemFolderColumn>
    <RevisionColumn>Revision</RevisionColumn>
    <FileNameColumn>FileName</FileNameColumn>
    <ExtensionColumn>Extension</ExtensionColumn>
    <DataSourceColumn>SourceCol</DataSourceColumn>
  </Columns>
</Configuration>";
        File.WriteAllText(tempXml, legacyContent);

        try
        {
            var config = xmlConfigService.LoadConfig(tempXml);
            Assert.Equal("SW-SolidWorks, OT-OpenText", config.DataSourceFilter);
            Assert.Equal("SourceCol", config.Columns.DataSourceColumn);
        }
        finally
        {
            if (File.Exists(tempXml)) File.Delete(tempXml);
        }
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

    [Fact]
    public async Task DataSourceFilter_SupportsMultipleValues()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FilepathMultiFilterTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "input_multi_filter.csv");
        string csv = "OldPath,ItemName,Revision,FileName,Extension,DataSource\n" +
            "D:\\Path1,ITEM1,0,ITEM1,SLDPRT,SW-SolidWorks\n" +
            "D:\\Path2,ITEM2,0,ITEM2,SLDPRT,OT-OpenText\n" +
            "D:\\Path3,ITEM3,0,ITEM3,SLDPRT,ACAD-AutoCAD\n" +
            "D:\\Path4,ITEM4,0,ITEM4,SLDPRT,INV-Inventor\n" +
            "D:\\Path5,ITEM5,0,ITEM5,SLDPRT,CREO-PTC\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Multi_Filtered_Output.txt",
            Delimiter = "|",
            CustomBasePath = @"I:\Data\Combined",
            // Comma and semicolon separated multi-value filter with extra spaces
            DataSourceFilter = "SW-SolidWorks, OT-OpenText; CREO-PTC",
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

        // Should include ITEM1, ITEM2, and ITEM5 (3 items), skipping ITEM3 and ITEM4
        Assert.Equal(3, count);
        string[] outputLines = await File.ReadAllLinesAsync(Path.Combine(tempDir, "Multi_Filtered_Output.txt"));
        Assert.Equal(3, outputLines.Length);
        Assert.Contains("ITEM1", outputLines[0]);
        Assert.Contains("ITEM2", outputLines[1]);
        Assert.Contains("ITEM5", outputLines[2]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void XmlConfigService_ParsesCustomDelimitersAndFileNameWithoutExtension()
    {
        var xmlConfigService = new XmlConfigService();
        string configPath = Path.GetFullPath(@"..\..\..\..\Genarate_OldVsNew_Filepaths.xml");

        var config = xmlConfigService.LoadConfig(configPath);

        Assert.Equal("OldPath", config.Columns.OldPathColumn);
        Assert.Equal("OldPath", config.Columns.SourcePathColumn);
        Assert.Equal("ItemName", config.Columns.ItemFolderColumn);
        Assert.Equal("Revision", config.Columns.RevisionColumn);
        Assert.Equal("FileName", config.Columns.FileNameColumn);
        Assert.Equal("Extension", config.Columns.ExtensionColumn);
        Assert.True(config.Columns.HasSeparateExtension);
        Assert.Equal(@"\", config.Columns.Delimiters.ItemToRevision);
        Assert.Equal(@"\", config.Columns.Delimiters.RevisionToFile);
        Assert.Equal(".", config.Columns.Delimiters.FileToExtension);
    }

    [Fact]
    public void XmlConfigService_ParsesFileNameWithExtensionOption()
    {
        var xmlConfigService = new XmlConfigService();
        string tempXml = Path.Combine(Path.GetTempPath(), "with_ext_config_" + Guid.NewGuid().ToString("N") + ".xml");
        string content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration>
  <OutputFileName>Output.txt</OutputFileName>
  <Delimiter>|</Delimiter>
  <CustomBasePath>D:\Base</CustomBasePath>
  <Columns>
    <SourcePathColumn>SourceFile</SourcePathColumn>
    <ItemFolderColumn>Folder</ItemFolderColumn>
    <Delimeter>\</Delimeter>
    <RevisionColumn>Rev</RevisionColumn>
    <Delimeter>\</Delimeter>
    <FileNameWithExtension>
      <FileNameColumn>FullFile</FileNameColumn>
    </FileNameWithExtension>
  </Columns>
</Configuration>";
        File.WriteAllText(tempXml, content);

        try
        {
            var config = xmlConfigService.LoadConfig(tempXml);
            Assert.Equal("SourceFile", config.Columns.OldPathColumn);
            Assert.Equal("Folder", config.Columns.ItemFolderColumn);
            Assert.Equal("Rev", config.Columns.RevisionColumn);
            Assert.Equal("FullFile", config.Columns.FileNameColumn);
            Assert.Null(config.Columns.ExtensionColumn);
            Assert.False(config.Columns.HasSeparateExtension);
        }
        finally
        {
            if (File.Exists(tempXml)) File.Delete(tempXml);
        }
    }

    [Fact]
    public async Task ProcessAsync_WithFileNameWithExtension_ProcessesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FilepathWithExtTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "input_with_ext.csv");
        // CSV only has FullFileName with extension (no separate Extension column)
        string csv = "SourcePath,Item,Rev,FullFileName\n" +
            "D:\\Old\\file1.sldprt,ITEM_A,0,partA.SLDPRT\n" +
            "D:\\Old\\file2.sldasm,ITEM_B,A,assemblyB.SLDASM\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Output_WithExt.txt",
            Delimiter = "|",
            CustomBasePath = @"C:\Dest\Data",
            Columns = new ColumnMapping
            {
                SourcePathColumn = "SourcePath",
                ItemFolderColumn = "Item",
                RevisionColumn = "Rev",
                FileNameColumn = "FullFileName",
                HasSeparateExtension = false
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(2, count);
        string[] outputLines = await File.ReadAllLinesAsync(Path.Combine(tempDir, "Output_WithExt.txt"));
        Assert.Equal(2, outputLines.Length);
        Assert.Equal(@"D:\Old\file1.sldprt|C:\Dest\Data\ITEM_A\0\partA.SLDPRT", outputLines[0]);
        Assert.Equal(@"D:\Old\file2.sldasm|C:\Dest\Data\ITEM_B\A\assemblyB.SLDASM", outputLines[1]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task GenericProject_AerospaceNX_ProcessesCustomColumnsAndFilters()
    {
        // Demonstrates completely custom column names and multi-filter in an Aerospace project
        string tempDir = Path.Combine(Path.GetTempPath(), "AerospaceTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "nx_input.csv");
        string csv = "OriginPath,AssemblyNo,RevCode,Model,Type,CADTool\n" +
            "D:\\Legacy\\wing.prt,ASY-1001,01,wing,prt,NX-Model\n" +
            "D:\\Legacy\\flap.drw,ASY-1002,02,flap,drw,NX-Draft\n" +
            "D:\\Legacy\\bolt.catpart,ASY-1003,01,bolt,catpart,CATIA\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Aerospace_Export.csv",
            Delimiter = ",",
            CustomBasePath = @"E:\Teamcenter\Storage\NX",
            DataSourceFilter = "NX-Model, NX-Draft",
            Columns = new ColumnMapping
            {
                SourcePathColumn = "OriginPath",
                ItemFolderColumn = "AssemblyNo",
                RevisionColumn = "RevCode",
                FileNameColumn = "Model",
                ExtensionColumn = "Type",
                DataSourceColumn = "CADTool",
                HasSeparateExtension = true
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        // 2 NX records should be matched and CATIA skipped
        Assert.Equal(2, count);
        string[] outputLines = await File.ReadAllLinesAsync(Path.Combine(tempDir, "Aerospace_Export.csv"));
        Assert.Equal(2, outputLines.Length);
        Assert.Equal(@"D:\Legacy\wing.prt,E:\Teamcenter\Storage\NX\ASY-1001\01\wing.prt", outputLines[0]);
        Assert.Equal(@"D:\Legacy\flap.drw,E:\Teamcenter\Storage\NX\ASY-1002\02\flap.drw", outputLines[1]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task GenericProject_CloudMigration_UsesForwardSlashesAndFileNameWithExt()
    {
        // Demonstrates Linux / Cloud forward-slash path migration with single full filename
        string tempDir = Path.Combine(Path.GetTempPath(), "CloudTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "cloud_input.csv");
        string csv = "SourceLocation,Folder,Version,FullName\n" +
            "/legacy/opt/data/doc1.pdf,PROJECT_A,V1,doc1.pdf\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Cloud_Output.txt",
            Delimiter = "\t",
            CustomBasePath = "/s3/bucket/archive",
            Columns = new ColumnMapping
            {
                SourcePathColumn = "SourceLocation",
                ItemFolderColumn = "Folder",
                RevisionColumn = "Version",
                FileNameColumn = "FullName",
                HasSeparateExtension = false,
                Delimiters = new PathDelimiters
                {
                    BaseToItem = "/",
                    ItemToRevision = "/",
                    RevisionToFile = "/"
                }
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(1, count);
        string[] outputLines = await File.ReadAllLinesAsync(Path.Combine(tempDir, "Cloud_Output.txt"));
        Assert.Equal("/legacy/opt/data/doc1.pdf\t/s3/bucket/archive/PROJECT_A/V1/doc1.pdf", outputLines[0]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task GenericProject_EmptyRevision_GeneratesCleanPathWithoutDoubleDelimiters()
    {
        // Demonstrates projects where revision is blank (omits revision folder cleanly)
        string tempDir = Path.Combine(Path.GetTempPath(), "NoRevTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "norev_input.csv");
        string csv = "OldPath,ItemName,Revision,FileName,Extension\n" +
            "D:\\Old\\doc.pdf,FOLDER_001,,doc,pdf\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "NoRev_Output.txt",
            Delimiter = "|",
            CustomBasePath = @"C:\Storage",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = string.Empty,
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension"
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(1, count);
        string[] outputLines = await File.ReadAllLinesAsync(Path.Combine(tempDir, "NoRev_Output.txt"));
        Assert.Equal(@"D:\Old\doc.pdf|C:\Storage\FOLDER_001\doc.pdf", outputLines[0]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task NegativeTest_MissingRequiredColumn_ThrowsInvalidOperationException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FailTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "bad_input.csv");
        // Missing "ItemFolder" column
        string csv = "OldPath,Revision,FileName,Extension\n" +
            "D:\\Old\\file.txt,0,file,txt\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Fail_Output.txt",
            Delimiter = "|",
            CustomBasePath = @"C:\Storage",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName", // Not in CSV!
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension"
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None));

        Assert.Contains("Required column 'ItemName' not found", ex.Message);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task NegativeTest_MissingDataSourceColumnWhenFiltering_ThrowsInvalidOperationException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FailFilterTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "nofiltercol_input.csv");
        string csv = "OldPath,ItemName,Revision,FileName,Extension\n" +
            "D:\\Old\\file.txt,ITEM1,0,file,txt\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Fail_Output.txt",
            Delimiter = "|",
            CustomBasePath = @"C:\Storage",
            DataSourceFilter = "SW-SolidWorks",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension",
                DataSourceColumn = "DataSource" // Not in CSV!
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None));

        Assert.Contains("DataSource column 'DataSource' not found for filtering", ex.Message);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void XmlConfigService_LoadsDelimiterAfterSourcePathColumn()
    {
        var xmlConfigService = new XmlConfigService();
        string configPath = Path.GetFullPath(@"..\..\..\..\Genarate_OldVsNew_Filepaths.xml");

        var config = xmlConfigService.LoadConfig(configPath);

        Assert.Equal("|", config.Delimiter);
        Assert.Equal("OldPath", config.Columns.SourcePathColumn);
        Assert.Equal(@"I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW", config.TargetRootFolder);
    }

    [Fact]
    public async Task GenericProject_WithExtraCustomColumnsAndDelimiters_ProcessesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ExtraColsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "extra_cols.csv");
        // CSV with extra columns: ProjectCode and Discipline
        string csv = "SourceFile,Plant,ProjectCode,Discipline,Rev,Part,Ext\n" +
            "D:\\Old\\p1.prt,PLANT_01,PRJ_ALPHA,MECHANICAL,A,pump_impeller,prt\n" +
            "D:\\Old\\p2.prt,PLANT_02,PRJ_BETA,ELECTRICAL,B,panel_cover,prt\n";
        await File.WriteAllTextAsync(inputFile, csv);

        string tempXml = Path.Combine(tempDir, "extra_cols_config.xml");
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration>
  <OutputFileName>Extra_Output.txt</OutputFileName>
  <Columns>
    <SourcePathColumn>SourceFile</SourcePathColumn>
    <Delimiter>::</Delimiter>

    <TargetRootFolder>X:\Enterprise\Vault</TargetRootFolder>
    <Delimeter>\</Delimeter>

    <PlantFolderColumn>Plant</PlantFolderColumn>
    <Delimeter>\</Delimeter>

    <ProjectFolderColumn>ProjectCode</ProjectFolderColumn>
    <Delimeter>\</Delimeter>

    <DisciplineFolderColumn>Discipline</DisciplineFolderColumn>
    <Delimeter>\</Delimeter>

    <RevisionColumn>Rev</RevisionColumn>
    <Delimeter>\</Delimeter>

    <FileNameWithoutExtension>
      <FileNameColumn>Part</FileNameColumn>
      <Delimeter>.</Delimeter>
      <ExtensionColumn>Ext</ExtensionColumn>
    </FileNameWithoutExtension>
  </Columns>
</Configuration>";
        await File.WriteAllTextAsync(tempXml, xml);

        var xmlConfigService = new XmlConfigService();
        var config = xmlConfigService.LoadConfig(tempXml);

        Assert.Equal("::", config.Delimiter);
        Assert.Equal(4, config.Columns.Segments.Count);

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(2, count);
        string[] outputLines = await File.ReadAllLinesAsync(Path.Combine(tempDir, "Extra_Output.txt"));
        Assert.Equal(2, outputLines.Length);

        string expectedLine1 = @"D:\Old\p1.prt::X:\Enterprise\Vault\PLANT_01\PRJ_ALPHA\MECHANICAL\A\pump_impeller.prt";
        string expectedLine2 = @"D:\Old\p2.prt::X:\Enterprise\Vault\PLANT_02\PRJ_BETA\ELECTRICAL\B\panel_cover.prt";

        Assert.Equal(expectedLine1, outputLines[0]);
        Assert.Equal(expectedLine2, outputLines[1]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ProcessAsync_WithExcelXlsxFile_ProcessesCorrectly()
    {
        string xlsxPath = Path.GetFullPath(@"..\..\..\..\Sample_Input.xlsx");
        Assert.True(File.Exists(xlsxPath), "Sample_Input.xlsx must exist.");

        var xmlConfigService = new XmlConfigService();
        string configPath = Path.GetFullPath(@"..\..\..\..\Genarate_OldVsNew_Filepaths.xml");
        var config = xmlConfigService.LoadConfig(configPath);

        // Filter by SolidWorks + Creo (6 rows total out of 10)
        config.DataSourceFilter = "SW-SolidWorks, CREO-PTC";

        var logger = new LoggerService();
        logger.Initialize(xlsxPath);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(xlsxPath, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(6, count);
        string outputPath = Path.Combine(Path.GetDirectoryName(xlsxPath)!, config.OutputFileName);
        Assert.True(File.Exists(outputPath));

        string[] outputLines = await File.ReadAllLinesAsync(outputPath);
        Assert.Equal(6, outputLines.Length);
    }

    [Fact]
    public async Task ProcessAsync_LogsSuccessAndFailedRowsWithReasons()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "RowLoggingTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "input_with_missing_values.csv");
        // Row 1: Valid
        // Row 2: Blank FileName and Revision
        // Row 3: Blank OldPath
        string csv = "OldPath,ItemName,Revision,FileName,Extension,DataSource\n" +
            "D:\\Path1\\file1.sldprt,ITEM_1,0,file1,sldprt,SW-SolidWorks\n" +
            "D:\\Path2\\file2.sldprt,ITEM_2,,,sldprt,SW-SolidWorks\n" +
            ",ITEM_3,A,file3,sldprt,SW-SolidWorks\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "RowLog_Output.txt",
            Delimiter = "|",
            CustomBasePath = @"C:\Vault\Data",
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

        // 1 success out of 3
        Assert.Equal(1, count);

        // Output file has 1 line
        string outputPath = Path.Combine(tempDir, "RowLog_Output.txt");
        string[] outputLines = await File.ReadAllLinesAsync(outputPath);
        Assert.Single(outputLines);

        // Verify Main Log
        Assert.True(File.Exists(logger.CurrentLogFilePath));
        string mainLog = await File.ReadAllTextAsync(logger.CurrentLogFilePath);
        Assert.Contains("[SUCCESS] [Row 2] SUCCESS:", mainLog);
        Assert.Contains("Row Data: [OldPath='D:\\Path1\\file1.sldprt'", mainLog);
        Assert.Contains("[FAILED ] [Row 3] FAILED: Missing or blank value in column(s): FileName, Revision | Row Data: [OldPath='D:\\Path2\\file2.sldprt'", mainLog);
        Assert.Contains("[FAILED ] [Row 4] FAILED: Missing or blank value in column(s): OldPath | Row Data: [OldPath='', ItemName='ITEM_3'", mainLog);
        Assert.Contains("Total Input Rows : 3", mainLog);
        Assert.Contains("Success (Written): 1", mainLog);
        Assert.Contains("Failed (Missing) : 2", mainLog);

        // Verify Success Log
        Assert.True(File.Exists(logger.SuccessLogFilePath));
        string successLog = await File.ReadAllTextAsync(logger.SuccessLogFilePath);
        Assert.Contains("[Row 2] SUCCESS:", successLog);
        Assert.Contains("file1.sldprt", successLog);
        Assert.Contains("Row Data: [OldPath='D:\\Path1\\file1.sldprt'", successLog);

        // Verify Failed Log
        Assert.True(File.Exists(logger.FailedLogFilePath));
        string failedLog = await File.ReadAllTextAsync(logger.FailedLogFilePath);
        Assert.Contains("[Row 3] FAILED: Missing or blank value in column(s): FileName, Revision | Row Data: [OldPath='D:\\Path2\\file2.sldprt'", failedLog);
        Assert.Contains("[Row 4] FAILED: Missing or blank value in column(s): OldPath | Row Data: [OldPath='', ItemName='ITEM_3'", failedLog);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ProcessAsync_LogsDetailedFailureWithMultiSegments()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SegmentLoggingTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "input_segments_missing.csv");
        // Row 2: Missing Plant and Part
        string csv = "SourceFile,Plant,ProjectCode,Discipline,Rev,Part,Ext\n" +
            "D:\\Old\\p1.prt,PLANT_01,PRJ_ALPHA,MECHANICAL,A,pump_impeller,prt\n" +
            "D:\\Old\\p2.prt,,PRJ_BETA,ELECTRICAL,B,,prt\n";
        await File.WriteAllTextAsync(inputFile, csv);

        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration>
  <OutputFileName>Segment_Output.txt</OutputFileName>
  <Columns>
    <SourcePathColumn>SourceFile</SourcePathColumn>
    <Delimiter>|</Delimiter>
    <TargetRootFolder>X:\Enterprise\Vault</TargetRootFolder>
    <Delimeter>\</Delimeter>
    <PlantFolderColumn>Plant</PlantFolderColumn>
    <Delimeter>\</Delimeter>
    <ProjectFolderColumn>ProjectCode</ProjectFolderColumn>
    <Delimeter>\</Delimeter>
    <DisciplineFolderColumn>Discipline</DisciplineFolderColumn>
    <Delimeter>\</Delimeter>
    <RevisionColumn>Rev</RevisionColumn>
    <Delimeter>\</Delimeter>
    <FileNameWithoutExtension>
      <FileNameColumn>Part</FileNameColumn>
      <Delimeter>.</Delimeter>
      <ExtensionColumn>Ext</ExtensionColumn>
    </FileNameWithoutExtension>
  </Columns>
</Configuration>";
        string tempXml = Path.Combine(tempDir, "config.xml");
        await File.WriteAllTextAsync(tempXml, xml);

        var xmlConfigService = new XmlConfigService();
        var config = xmlConfigService.LoadConfig(tempXml);

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(1, count);

        // Verify Failed Log has reason with Plant and Part + complete row data
        string failedLog = await File.ReadAllTextAsync(logger.FailedLogFilePath);
        Assert.Contains("[Row 3] FAILED: Missing or blank value in column(s): Part, Plant | Row Data: [SourceFile='D:\\Old\\p2.prt', Plant='', ProjectCode='PRJ_BETA', Discipline='ELECTRICAL', Rev='B', Part='', Ext='prt']", failedLog);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task GenerateSampleLogs_InSampleRunDirectory()
    {
        string sampleRunDir = Path.GetFullPath(@"..\..\..\..\SampleRun");
        if (!Directory.Exists(sampleRunDir)) Directory.CreateDirectory(sampleRunDir);

        string inputFile = Path.Combine(sampleRunDir, "Sample_Input_With_Errors.csv");
        string csv = "OldPath,ItemName,Revision,FileName,Extension,DataSource\n" +
            "D:\\Source\\cryo_pump.sldprt,ITEM_1001,0,cryo_pump,sldprt,SW-SolidWorks\n" +
            "D:\\Source\\assembly_housing.sldasm,ITEM_1002,A,assembly_housing,sldasm,SW-SolidWorks\n" +
            "D:\\Source\\corrupted_file.sldprt,ITEM_1003,,corrupted_file,sldprt,SW-SolidWorks\n" +
            ",ITEM_1004,B,valve_body,sldprt,SW-SolidWorks\n" +
            "D:\\Source\\missing_name.sldprt,ITEM_1005,0,,sldprt,SW-SolidWorks\n" +
            "D:\\Source\\autocad_schematic.dwg,ITEM_2001,01,autocad_schematic,dwg,ACAD-AutoCAD\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Output_Mapping.txt",
            Delimiter = "|",
            TargetRootFolder = @"I:\PROLIM_DM\20260504\2_T\01_02_SW_OT_Combined\Data\SW",
            DataSourceFilter = "SW-SolidWorks",
            Columns = new ColumnMapping
            {
                SourcePathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension",
                DataSourceColumn = "DataSource",
                HasSeparateExtension = true
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);
    }

    [Fact]
    public void XmlConfigService_LoadsNewFilePathColumnAndInsertAfterColumn()
    {
        var xmlConfigService = new XmlConfigService();
        string configPath = Path.GetFullPath(@"..\..\..\..\Genarate_OldVsNew_Filepaths.xml");
        var config = xmlConfigService.LoadConfig(configPath);

        Assert.Equal("NewFilePath", config.Columns.NewFilePathColumn);
        Assert.Equal("OldPath", config.Columns.InsertAfterColumn);
    }

    [Fact]
    public void TableExportService_DetermineColumnOrder_InsertsCorrectly()
    {
        var exporter = new TableExportService();
        var dt = new DataTable();
        dt.Columns.Add("OldPath");
        dt.Columns.Add("ItemName");
        dt.Columns.Add("Revision");
        dt.Columns.Add("FileName");
        dt.Columns.Add("Extension");

        // Insert after OldPath
        var cols1 = exporter.DetermineColumnOrder(dt, "NewFilePath", "OldPath");
        Assert.Equal(new[] { "OldPath", "NewFilePath", "ItemName", "Revision", "FileName", "Extension" }, cols1);

        // Insert after Revision
        var cols2 = exporter.DetermineColumnOrder(dt, "NewFilePath", "Revision");
        Assert.Equal(new[] { "OldPath", "ItemName", "Revision", "NewFilePath", "FileName", "Extension" }, cols2);

        // Insert after non-existent column -> appends to end
        var cols3 = exporter.DetermineColumnOrder(dt, "NewFilePath", "NonExistentColumn");
        Assert.Equal(new[] { "OldPath", "ItemName", "Revision", "FileName", "Extension", "NewFilePath" }, cols3);

        // No insertAfter -> appends to end
        var cols4 = exporter.DetermineColumnOrder(dt, "NewFilePath", null);
        Assert.Equal(new[] { "OldPath", "ItemName", "Revision", "FileName", "Extension", "NewFilePath" }, cols4);
    }

    [Fact]
    public async Task ProcessAsync_GeneratesUpdatedCsv_WithNewFilePathInsertedAfterOldPath()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "UpdatedCsvTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "input_for_update.csv");
        string csv = "OldPath,ItemName,Revision,FileName,Extension,DataSource\n" +
            "D:\\Old\\file1.sldprt,ITEM_001,0,file1,sldprt,SW-SolidWorks\n" +
            "D:\\Old\\file2.sldasm,ITEM_002,A,file2,sldasm,SW-SolidWorks\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Output.txt",
            UpdatedInputFileName = "Custom_Updated_Input.csv",
            Delimiter = "|",
            TargetRootFolder = @"E:\Target\Vault",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension",
                DataSourceColumn = "DataSource",
                NewFilePathColumn = "TargetFilePath",
                InsertAfterColumn = "OldPath"
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal(2, count);

        // Verify primary Output.txt
        string outPath = Path.Combine(tempDir, "Output.txt");
        Assert.True(File.Exists(outPath));

        // Verify Custom_Updated_Input.csv
        string updatedFile = Path.Combine(tempDir, "Custom_Updated_Input.csv");
        Assert.True(File.Exists(updatedFile));

        string[] lines = await File.ReadAllLinesAsync(updatedFile);
        Assert.Equal(3, lines.Length);
        Assert.Equal("OldPath,TargetFilePath,ItemName,Revision,FileName,Extension,DataSource", lines[0]);
        Assert.Equal(@"D:\Old\file1.sldprt,E:\Target\Vault\ITEM_001\0\file1.sldprt,ITEM_001,0,file1,sldprt,SW-SolidWorks", lines[1]);
        Assert.Equal(@"D:\Old\file2.sldasm,E:\Target\Vault\ITEM_002\A\file2.sldasm,ITEM_002,A,file2,sldasm,SW-SolidWorks", lines[2]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ProcessAsync_GeneratesUpdatedCsv_WithNewFilePathAppendedAtEnd()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "UpdatedCsvEndTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "data.csv");
        string csv = "OldPath,ItemName,Revision,FileName,Extension\n" +
            "D:\\Old\\doc.pdf,FOLDER1,0,doc,pdf\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Output.txt",
            Delimiter = "|",
            TargetRootFolder = @"C:\Vault",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension",
                NewFilePathColumn = "CalculatedNewPath",
                InsertAfterColumn = null // Should append at end
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);
        Assert.Equal(1, count);

        // Default naming: data_Updated.csv
        string defaultUpdatedFile = Path.Combine(tempDir, "data_Updated.csv");
        Assert.True(File.Exists(defaultUpdatedFile));

        string[] lines = await File.ReadAllLinesAsync(defaultUpdatedFile);
        Assert.Equal("OldPath,ItemName,Revision,FileName,Extension,CalculatedNewPath", lines[0]);
        Assert.Equal(@"D:\Old\doc.pdf,FOLDER1,0,doc,pdf,C:\Vault\FOLDER1\0\doc.pdf", lines[1]);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ProcessAsync_GeneratesUpdatedXlsx_WithNewFilePathInsertedAfterFileName()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "UpdatedXlsxTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "parts.xlsx");
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("PartsData");
            ws.Cell(1, 1).Value = "OldPath";
            ws.Cell(1, 2).Value = "ItemName";
            ws.Cell(1, 3).Value = "Revision";
            ws.Cell(1, 4).Value = "FileName";
            ws.Cell(1, 5).Value = "Extension";

            ws.Cell(2, 1).Value = @"D:\Old\part1.prt";
            ws.Cell(2, 2).Value = "PART-100";
            ws.Cell(2, 3).Value = "01";
            ws.Cell(2, 4).Value = "part1";
            ws.Cell(2, 5).Value = "prt";

            wb.SaveAs(inputFile);
        }

        var config = new AppConfig
        {
            OutputFileName = "Output.txt",
            Delimiter = "|",
            ExcelSheetName = "PartsData",
            TargetRootFolder = @"E:\Teamcenter\Storage",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension",
                NewFilePathColumn = "TargetFilePath",
                InsertAfterColumn = "FileName"
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);
        Assert.Equal(1, count);

        // Expected updated file: parts_Updated.xlsx
        string updatedFile = Path.Combine(tempDir, "parts_Updated.xlsx");
        Assert.True(File.Exists(updatedFile));

        // Read back with ClosedXML to verify Excel structure
        using (var updatedWb = new ClosedXML.Excel.XLWorkbook(updatedFile))
        {
            Assert.True(updatedWb.Worksheets.Contains("PartsData"));
            var ws = updatedWb.Worksheet("PartsData");

            // Columns should be: OldPath (1), ItemName (2), Revision (3), FileName (4), TargetFilePath (5), Extension (6)
            Assert.Equal("OldPath", ws.Cell(1, 1).GetString());
            Assert.Equal("ItemName", ws.Cell(1, 2).GetString());
            Assert.Equal("Revision", ws.Cell(1, 3).GetString());
            Assert.Equal("FileName", ws.Cell(1, 4).GetString());
            Assert.Equal("TargetFilePath", ws.Cell(1, 5).GetString());
            Assert.Equal("Extension", ws.Cell(1, 6).GetString());

            // Row 2 values
            Assert.Equal(@"D:\Old\part1.prt", ws.Cell(2, 1).GetString());
            Assert.Equal("PART-100", ws.Cell(2, 2).GetString());
            Assert.Equal("01", ws.Cell(2, 3).GetString());
            Assert.Equal("part1", ws.Cell(2, 4).GetString());
            Assert.Equal(@"E:\Teamcenter\Storage\PART-100\01\part1.prt", ws.Cell(2, 5).GetString());
            Assert.Equal("prt", ws.Cell(2, 6).GetString());
        }

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ProcessAsync_UpdatedFile_PreservesSkippedAndFailedRowsWithBlankNewFilePath()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "PreserveRowsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string inputFile = Path.Combine(tempDir, "mixed_input.csv");
        // Row 1: Valid SW
        // Row 2: Filtered out (ACAD)
        // Row 3: Missing FileName (Failed)
        string csv = "OldPath,ItemName,Revision,FileName,Extension,DataSource\n" +
            "D:\\Old\\file1.sldprt,ITEM_1,0,file1,sldprt,SW-SolidWorks\n" +
            "D:\\Old\\file2.dwg,ITEM_2,A,file2,dwg,ACAD-AutoCAD\n" +
            "D:\\Old\\file3.sldprt,ITEM_3,B,,sldprt,SW-SolidWorks\n";
        await File.WriteAllTextAsync(inputFile, csv);

        var config = new AppConfig
        {
            OutputFileName = "Output.txt",
            Delimiter = "|",
            TargetRootFolder = @"C:\Vault",
            DataSourceFilter = "SW-SolidWorks",
            Columns = new ColumnMapping
            {
                OldPathColumn = "OldPath",
                ItemFolderColumn = "ItemName",
                RevisionColumn = "Revision",
                FileNameColumn = "FileName",
                ExtensionColumn = "Extension",
                DataSourceColumn = "DataSource",
                NewFilePathColumn = "NewPath",
                InsertAfterColumn = "OldPath"
            }
        };

        var logger = new LoggerService();
        logger.Initialize(inputFile);
        var processor = new Services.Genarate_OldVsNew_Filepaths();

        int count = await processor.ProcessAsync(inputFile, config, logger, new Progress<string>(_ => { }), CancellationToken.None);
        Assert.Equal(1, count); // Only 1 success

        string updatedFile = Path.Combine(tempDir, "mixed_input_Updated.csv");
        Assert.True(File.Exists(updatedFile));

        string[] lines = await File.ReadAllLinesAsync(updatedFile);
        Assert.Equal(4, lines.Length); // Header + 3 rows

        // Header
        Assert.Equal("OldPath,NewPath,ItemName,Revision,FileName,Extension,DataSource", lines[0]);

        // Row 1: Success has NewPath filled in
        Assert.Equal(@"D:\Old\file1.sldprt,C:\Vault\ITEM_1\0\file1.sldprt,ITEM_1,0,file1,sldprt,SW-SolidWorks", lines[1]);

        // Row 2: Skipped (ACAD) preserves original data, NewPath is empty
        Assert.Equal("D:\\Old\\file2.dwg,,ITEM_2,A,file2,dwg,ACAD-AutoCAD", lines[2]);

        // Row 3: Failed (Missing FileName) preserves original data, NewPath is empty
        Assert.Equal("D:\\Old\\file3.sldprt,,ITEM_3,B,,sldprt,SW-SolidWorks", lines[3]);

        Directory.Delete(tempDir, true);
    }
}
