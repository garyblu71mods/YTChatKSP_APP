namespace YTchatKSP_App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

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
        var labelTitle = new Label();
        labelTitle.Text = "YouTube Chat Server";
        labelTitle.Font = new Font("Arial", 16, FontStyle.Bold);
        labelTitle.Location = new Point(12, 12);
        labelTitle.AutoSize = true;

        var labelVideoInput = new Label();
        labelVideoInput.Text = "YouTube Video ID or URL:";
        labelVideoInput.Location = new Point(12, 50);
        labelVideoInput.AutoSize = true;

        textBoxVideoInput = new TextBox();
        textBoxVideoInput.Location = new Point(12, 70);
        textBoxVideoInput.Width = 500;
        textBoxVideoInput.Height = 30;

        labelStatus = new Label();
        labelStatus.Text = "Status: Disconnected";
        labelStatus.Location = new Point(12, 105);
        labelStatus.Width = 300;
        labelStatus.Height = 30;
        labelStatus.BorderStyle = BorderStyle.FixedSingle;
        labelStatus.Padding = new Padding(5);
        labelStatus.TextAlign = ContentAlignment.MiddleLeft;
        labelStatus.BackColor = Color.LightCoral;

        buttonConnect = new Button();
        buttonConnect.Text = "Connect";
        buttonConnect.Location = new Point(12, 145);
        buttonConnect.Width = 120;
        buttonConnect.Height = 30;
        buttonConnect.Click += ButtonConnect_Click;

        buttonDisconnect = new Button();
        buttonDisconnect.Text = "Disconnect";
        buttonDisconnect.Location = new Point(140, 145);
        buttonDisconnect.Width = 120;
        buttonDisconnect.Height = 30;
        buttonDisconnect.Enabled = false;
        buttonDisconnect.Click += ButtonDisconnect_Click;

        buttonClear = new Button();
        buttonClear.Text = "Clear";
        buttonClear.Location = new Point(268, 145);
        buttonClear.Width = 120;
        buttonClear.Height = 30;
        buttonClear.Click += ButtonClear_Click;

        var labelMessages = new Label();
        labelMessages.Text = "Live Messages:";
        labelMessages.Location = new Point(12, 185);
        labelMessages.AutoSize = true;

        listBoxMessages = new ListBox();
        listBoxMessages.Location = new Point(12, 205);
        listBoxMessages.Width = 518;
        listBoxMessages.Height = 150;

        labelMessageCount = new Label();
        labelMessageCount.Text = "Messages: 0";
        labelMessageCount.Location = new Point(12, 365);
        labelMessageCount.AutoSize = true;

        var labelApiStatus = new Label();
        labelApiStatus.Text = "API Status:";
        labelApiStatus.Location = new Point(12, 390);
        labelApiStatus.AutoSize = true;

        labelApiInfo = new Label();
        labelApiInfo.Text = "API ready on http://localhost:5000";
        labelApiInfo.Location = new Point(12, 415);
        labelApiInfo.Width = 518;
        labelApiInfo.Height = 50;
        labelApiInfo.BorderStyle = BorderStyle.FixedSingle;
        labelApiInfo.Padding = new Padding(5);

        var labelLog = new Label();
        labelLog.Text = "Log:";
        labelLog.Location = new Point(12, 473);
        labelLog.AutoSize = true;

        textBoxLog = new TextBox();
        textBoxLog.Location = new Point(12, 493);
        textBoxLog.Width = 518;
        textBoxLog.Height = 85;
        textBoxLog.Multiline = true;
        textBoxLog.ScrollBars = ScrollBars.Vertical;
        textBoxLog.ReadOnly = true;

        this.ClientSize = new Size(545, 590);
        this.Text = "YouTubeChatServer";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Load += MainForm_Load;

        this.Controls.Add(labelTitle);
        this.Controls.Add(labelVideoInput);
        this.Controls.Add(textBoxVideoInput);
        this.Controls.Add(labelStatus);
        this.Controls.Add(buttonConnect);
        this.Controls.Add(buttonDisconnect);
        this.Controls.Add(buttonClear);
        this.Controls.Add(labelMessages);
        this.Controls.Add(listBoxMessages);
        this.Controls.Add(labelMessageCount);
        this.Controls.Add(labelApiStatus);
        this.Controls.Add(labelApiInfo);
        this.Controls.Add(labelLog);
        this.Controls.Add(textBoxLog);
    }

    private TextBox textBoxVideoInput;
    private Label labelStatus;
    private Button buttonConnect;
    private Button buttonDisconnect;
    private Button buttonClear;
    private ListBox listBoxMessages;
    private Label labelMessageCount;
    private Label labelApiInfo;
    private TextBox textBoxLog;
}
