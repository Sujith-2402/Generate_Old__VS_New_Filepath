namespace Genarate_OldVsNew_Filepaths.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private Label lblInput;
    private TextBox txtInputFile;
    private Button btnBrowseInput;
    private Label lblConfig;
    private TextBox txtConfigFile;
    private Button btnBrowseConfig;
    private RadioButton rbGenOldVsNew;
    private Button btnStart;
    private Button btnCancel;
    private Button btnViewLogs;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblStatus;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblInput = new Label();
        txtInputFile = new TextBox();
        btnBrowseInput = new Button();
        lblConfig = new Label();
        txtConfigFile = new TextBox();
        btnBrowseConfig = new Button();
        rbGenOldVsNew = new RadioButton();
        btnStart = new Button();
        btnCancel = new Button();
        btnViewLogs = new Button();
        statusStrip = new StatusStrip();
        lblStatus = new ToolStripStatusLabel();
        statusStrip.SuspendLayout();
        SuspendLayout();

        // lblInput
        lblInput.AutoSize = true;
        lblInput.Location = new Point(20, 20);
        lblInput.Text = "Input File (Excel):";
        // txtInputFile
        txtInputFile.Location = new Point(20, 42);
        txtInputFile.Size = new Size(500, 25);
        // btnBrowseInput
        btnBrowseInput.Location = new Point(530, 40);
        btnBrowseInput.Size = new Size(100, 28);
        btnBrowseInput.Text = "Browse...";
        btnBrowseInput.Click += BtnBrowseInput_Click;

        // lblConfig
        lblConfig.AutoSize = true;
        lblConfig.Location = new Point(20, 85);
        lblConfig.Text = "Configuration File (XML):";
        // txtConfigFile
        txtConfigFile.Location = new Point(20, 107);
        txtConfigFile.Size = new Size(500, 25);
        // btnBrowseConfig
        btnBrowseConfig.Location = new Point(530, 105);
        btnBrowseConfig.Size = new Size(100, 28);
        btnBrowseConfig.Text = "Browse...";
        btnBrowseConfig.Click += BtnBrowseConfig_Click;

        // rbGenOldVsNew
        rbGenOldVsNew.AutoSize = true;
        rbGenOldVsNew.Checked = true;
        rbGenOldVsNew.Location = new Point(20, 150);
        rbGenOldVsNew.Text = "Genarate_OldVsNew_Filepaths";
        rbGenOldVsNew.Font = new Font(Font, FontStyle.Bold);

        // btnStart
        btnStart.Location = new Point(290, 195);
        btnStart.Size = new Size(105, 32);
        btnStart.Text = "Start";
        btnStart.Click += BtnStart_Click;

        // btnCancel
        btnCancel.Location = new Point(410, 195);
        btnCancel.Size = new Size(105, 32);
        btnCancel.Text = "Cancel";
        btnCancel.Enabled = false;
        btnCancel.Click += BtnCancel_Click;

        // btnViewLogs
        btnViewLogs.Location = new Point(530, 195);
        btnViewLogs.Size = new Size(100, 32);
        btnViewLogs.Text = "View Logs";
        btnViewLogs.Enabled = false;
        btnViewLogs.Click += BtnViewLogs_Click;

        // statusStrip
        statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
        statusStrip.Location = new Point(0, 245);
        lblStatus.Text = "Ready";

        // MainForm
        ClientSize = new Size(655, 270);
        Controls.AddRange(new Control[] {
            lblInput, txtInputFile, btnBrowseInput,
            lblConfig, txtConfigFile, btnBrowseConfig,
            rbGenOldVsNew, btnStart, btnCancel, btnViewLogs, statusStrip
        });
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Generate Old vs New Filepaths Utility";
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
