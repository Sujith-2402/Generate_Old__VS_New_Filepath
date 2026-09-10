using System.Diagnostics;
using Genarate_OldVsNew_Filepaths.Services;

namespace Genarate_OldVsNew_Filepaths.UI;

public partial class MainForm : Form
{
    private readonly XmlConfigService _xmlConfig = new();
    private readonly Services.Genarate_OldVsNew_Filepaths _processor = new();
    private readonly LoggerService _logger = new();
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        InitializeComponent();
    }

    private void BtnBrowseInput_Click(object? sender, EventArgs e) =>
        BrowseFile("Input Files (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*", txtInputFile);

    private void BtnBrowseConfig_Click(object? sender, EventArgs e) =>
        BrowseFile("XML Files (*.xml)|*.xml|All Files (*.*)|*.*", txtConfigFile);

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (!ValidateInputs()) return;
        _cts = new CancellationTokenSource();
        SetUiState(true);
        try
        {
            _logger.Initialize(txtInputFile.Text);
            btnViewLogs.Enabled = true;
            var config = _xmlConfig.LoadConfig(txtConfigFile.Text);
            var progress = new Progress<string>(msg => lblStatus.Text = msg);
            int count = await _processor.ProcessAsync(txtInputFile.Text, config, _logger, progress, _cts.Token);
            lblStatus.Text = $"Done! {count} lines written.";
            MessageBox.Show($"File generated successfully!\nTotal rows: {count}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            lblStatus.Text = "Process cancelled by user.";
            _logger.LogWarning("Process was cancelled by the user.");
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Error occurred.";
            _logger.LogError("Processing failed", ex);
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetUiState(false);
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        lblStatus.Text = "Cancelling...";
    }

    private void BtnViewLogs_Click(object? sender, EventArgs e)
    {
        if (File.Exists(_logger.CurrentLogFilePath))
            Process.Start(new ProcessStartInfo { FileName = _logger.CurrentLogFilePath, UseShellExecute = true });
    }

    private bool ValidateInputs()
    {
        if (!rbGenOldVsNew.Checked) return ShowWarning("Please select 'Genarate_OldVsNew_Filepaths'.");
        if (!File.Exists(txtInputFile.Text)) return ShowWarning("Please select a valid input Excel/CSV file.");
        if (!File.Exists(txtConfigFile.Text)) return ShowWarning("Please select a valid XML configuration file.");
        return true;
    }

    private bool ShowWarning(string message)
    {
        MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void BrowseFile(string filter, TextBox target)
    {
        using var ofd = new OpenFileDialog { Filter = filter };
        if (ofd.ShowDialog() == DialogResult.OK) target.Text = ofd.FileName;
    }

    private void SetUiState(bool isRunning)
    {
        btnStart.Enabled = btnBrowseInput.Enabled = btnBrowseConfig.Enabled = !isRunning;
        btnCancel.Enabled = isRunning;
    }
}
