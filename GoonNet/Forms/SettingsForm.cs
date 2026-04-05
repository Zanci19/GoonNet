using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public class SettingsForm : Form
{
    private TabControl _tabs = null!;
    private Button _btnOK = null!;
    private Button _btnCancel = null!;

    // Audio tab
    private TextBox _txtMainDevice = null!;
    private TextBox _txtPreviewDevice = null!;

    // Email tab
    private TextBox _txtSmtpHost = null!;
    private NumericUpDown _nudSmtpPort = null!;
    private TextBox _txtSmtpUser = null!;
    private TextBox _txtSmtpPass = null!;
    private TextBox _txtEmailFrom = null!;

    // Paths tab
    private TextBox _txtMusicPath = null!;

    // MySQL tab
    private TextBox _txtMySqlServer = null!;
    private NumericUpDown _nudMySqlPort = null!;
    private TextBox _txtMySqlDatabase = null!;
    private TextBox _txtMySqlUser = null!;
    private TextBox _txtMySqlPassword = null!;
    private Label _lblTestResult = null!;

    public SettingsForm()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text = "Settings";
        Size = new Size(460, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        _tabs = new TabControl { Location = new Point(8, 8), Size = new Size(432, 310) };

        // Audio tab
        var audioPage = new TabPage("Audio");
        AddLabelTextBox(audioPage, "Main Device:", ref _txtMainDevice, 8, 16);
        AddLabelTextBox(audioPage, "Preview Device:", ref _txtPreviewDevice, 8, 42);
        _tabs.TabPages.Add(audioPage);

        // Email tab
        var emailPage = new TabPage("Email");
        AddLabelTextBox(emailPage, "SMTP Host:", ref _txtSmtpHost, 8, 16);
        _nudSmtpPort = new NumericUpDown { Location = new Point(130, 42), Size = new Size(80, 20), Minimum = 1, Maximum = 65535, Value = 25 };
        emailPage.Controls.Add(new Label { Text = "SMTP Port:", Location = new Point(8, 45), Size = new Size(120, 16), TextAlign = ContentAlignment.MiddleRight });
        emailPage.Controls.Add(_nudSmtpPort);
        AddLabelTextBox(emailPage, "Username:", ref _txtSmtpUser, 8, 68);
        _txtSmtpPass = new TextBox { Location = new Point(130, 94), Size = new Size(280, 20), BorderStyle = BorderStyle.Fixed3D, PasswordChar = '*' };
        emailPage.Controls.Add(new Label { Text = "Password:", Location = new Point(8, 97), Size = new Size(120, 16), TextAlign = ContentAlignment.MiddleRight });
        emailPage.Controls.Add(_txtSmtpPass);
        AddLabelTextBox(emailPage, "From Address:", ref _txtEmailFrom, 8, 120);
        _tabs.TabPages.Add(emailPage);

        // Paths tab
        var pathsPage = new TabPage("Paths");
        AddLabelTextBox(pathsPage, "Music Folder:", ref _txtMusicPath, 8, 16);
        var btnBrowse = new Button { Text = "...", Location = new Point(415, 16), Size = new Size(24, 20), FlatStyle = FlatStyle.System };
        btnBrowse.Click += BtnBrowseMusicPath_Click;
        pathsPage.Controls.Add(btnBrowse);
        _tabs.TabPages.Add(pathsPage);

        // MySQL tab
        var mysqlPage = new TabPage("MySQL");
        AddLabelTextBox(mysqlPage, "Server:", ref _txtMySqlServer, 8, 16);
        _nudMySqlPort = new NumericUpDown { Location = new Point(130, 42), Size = new Size(80, 20), Minimum = 1, Maximum = 65535, Value = 3306 };
        mysqlPage.Controls.Add(new Label { Text = "Port:", Location = new Point(8, 45), Size = new Size(120, 16), TextAlign = ContentAlignment.MiddleRight });
        mysqlPage.Controls.Add(_nudMySqlPort);
        AddLabelTextBox(mysqlPage, "Database:", ref _txtMySqlDatabase, 8, 68);
        AddLabelTextBox(mysqlPage, "Username:", ref _txtMySqlUser, 8, 94);
        _txtMySqlPassword = new TextBox { Location = new Point(130, 120), Size = new Size(280, 20), BorderStyle = BorderStyle.Fixed3D, PasswordChar = '*' };
        mysqlPage.Controls.Add(new Label { Text = "Password:", Location = new Point(8, 123), Size = new Size(120, 16), TextAlign = ContentAlignment.MiddleRight });
        mysqlPage.Controls.Add(_txtMySqlPassword);
        var btnTest = new Button { Text = "Test Connection", Location = new Point(130, 148), Size = new Size(120, 24), FlatStyle = FlatStyle.System };
        btnTest.Click += BtnTestMySql_Click;
        _lblTestResult = new Label { Text = "", Location = new Point(130, 178), Size = new Size(280, 30), ForeColor = Color.DarkGreen };
        mysqlPage.Controls.Add(btnTest);
        mysqlPage.Controls.Add(_lblTestResult);
        _tabs.TabPages.Add(mysqlPage);

        _btnOK = new Button { Text = "OK", Location = new Point(280, 328), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnOK.Click += BtnOK_Click;
        _btnCancel = new Button { Text = "Cancel", Location = new Point(358, 328), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;
        Controls.AddRange(new Control[] { _tabs, _btnOK, _btnCancel });
    }

    private static void AddLabelTextBox(TabPage page, string label, ref TextBox tb, int x, int y)
    {
        page.Controls.Add(new Label { Text = label, Location = new Point(x, y + 3), Size = new Size(120, 16), TextAlign = ContentAlignment.MiddleRight });
        tb = new TextBox { Location = new Point(x + 124, y), Size = new Size(280, 20), BorderStyle = BorderStyle.Fixed3D };
        page.Controls.Add(tb);
    }

    private void LoadSettings()
    {
        var s = AppSettings.Instance;
        _txtMainDevice.Text = s.MainAudioDevice;
        _txtPreviewDevice.Text = s.PreviewAudioDevice;
        _txtSmtpHost.Text = s.SmtpHost;
        _nudSmtpPort.Value = Math.Clamp(s.SmtpPort, 1, 65535);
        _txtSmtpUser.Text = s.SmtpUser;
        _txtSmtpPass.Text = s.SmtpPassword;
        _txtEmailFrom.Text = s.EmailFrom;
        _txtMusicPath.Text = s.MusicFolder;
        _txtMySqlServer.Text = s.MySqlServer;
        _nudMySqlPort.Value = Math.Clamp(s.MySqlPort, 1, 65535);
        _txtMySqlDatabase.Text = s.MySqlDatabase;
        _txtMySqlUser.Text = s.MySqlUser;
        _txtMySqlPassword.Text = s.MySqlPassword;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        var s = AppSettings.Instance;
        s.MainAudioDevice = _txtMainDevice.Text.Trim();
        s.PreviewAudioDevice = _txtPreviewDevice.Text.Trim();
        s.SmtpHost = _txtSmtpHost.Text.Trim();
        s.SmtpPort = (int)_nudSmtpPort.Value;
        s.SmtpUser = _txtSmtpUser.Text.Trim();
        s.SmtpPassword = _txtSmtpPass.Text;
        s.EmailFrom = _txtEmailFrom.Text.Trim();
        s.MusicFolder = _txtMusicPath.Text.Trim();
        s.MySqlServer = _txtMySqlServer.Text.Trim();
        s.MySqlPort = (int)_nudMySqlPort.Value;
        s.MySqlDatabase = _txtMySqlDatabase.Text.Trim();
        s.MySqlUser = _txtMySqlUser.Text.Trim();
        s.MySqlPassword = _txtMySqlPassword.Text;
        s.Save();
    }

    private void BtnBrowseMusicPath_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select Music Folder" };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtMusicPath.Text = dlg.SelectedPath;
    }

    private void BtnTestMySql_Click(object? sender, EventArgs e)
    {
        var connStr = $"Server={_txtMySqlServer.Text.Trim()};Port={_nudMySqlPort.Value};" +
                      $"Database={_txtMySqlDatabase.Text.Trim()};User ID={_txtMySqlUser.Text.Trim()};" +
                      $"Password={_txtMySqlPassword.Text};AllowZeroDateTime=True;ConvertZeroDateTime=True;";
        var testDb = new MySqlMusicDatabase();
        testDb.InitializeMySql(connStr);
        if (testDb.TestConnection(out var err))
        {
            _lblTestResult.ForeColor = Color.DarkGreen;
            _lblTestResult.Text = "✓ Connection successful!";
        }
        else
        {
            _lblTestResult.ForeColor = Color.Red;
            _lblTestResult.Text = "✗ " + err;
        }
    }
}
