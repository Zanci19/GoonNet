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

    public SettingsForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Settings";
        Size = new Size(420, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        _tabs = new TabControl { Location = new Point(8, 8), Size = new Size(390, 260) };

        // Audio tab
        var audioPage = new TabPage("Audio");
        AddLabelTextBox(audioPage, "Main Device:", ref _txtMainDevice, 8, 16);
        AddLabelTextBox(audioPage, "Preview Device:", ref _txtPreviewDevice, 8, 42);
        _tabs.TabPages.Add(audioPage);

        // Email tab
        var emailPage = new TabPage("Email");
        AddLabelTextBox(emailPage, "SMTP Host:", ref _txtSmtpHost, 8, 16);
        _nudSmtpPort = new NumericUpDown { Location = new Point(120, 42), Size = new Size(80, 20), Minimum = 1, Maximum = 65535, Value = 25 };
        emailPage.Controls.Add(new Label { Text = "SMTP Port:", Location = new Point(8, 45), Size = new Size(110, 16), TextAlign = ContentAlignment.MiddleRight });
        emailPage.Controls.Add(_nudSmtpPort);
        AddLabelTextBox(emailPage, "Username:", ref _txtSmtpUser, 8, 68);
        _txtSmtpPass = new TextBox { Location = new Point(120, 94), Size = new Size(250, 20), BorderStyle = BorderStyle.Fixed3D, PasswordChar = '*' };
        emailPage.Controls.Add(new Label { Text = "Password:", Location = new Point(8, 97), Size = new Size(110, 16), TextAlign = ContentAlignment.MiddleRight });
        emailPage.Controls.Add(_txtSmtpPass);
        AddLabelTextBox(emailPage, "From Address:", ref _txtEmailFrom, 8, 120);
        _tabs.TabPages.Add(emailPage);

        // Paths tab
        var pathsPage = new TabPage("Paths");
        AddLabelTextBox(pathsPage, "Music Folder:", ref _txtMusicPath, 8, 16);
        var btnBrowse = new Button { Text = "...", Location = new Point(374, 16), Size = new Size(24, 20), FlatStyle = FlatStyle.System };
        btnBrowse.Click += BtnBrowseMusicPath_Click;
        pathsPage.Controls.Add(btnBrowse);
        _tabs.TabPages.Add(pathsPage);

        _btnOK = new Button { Text = "OK", Location = new Point(240, 278), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnCancel = new Button { Text = "Cancel", Location = new Point(318, 278), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;
        Controls.AddRange(new Control[] { _tabs, _btnOK, _btnCancel });
    }

    private static void AddLabelTextBox(TabPage page, string label, ref TextBox tb, int x, int y)
    {
        page.Controls.Add(new Label { Text = label, Location = new Point(x, y + 3), Size = new Size(110, 16), TextAlign = ContentAlignment.MiddleRight });
        tb = new TextBox { Location = new Point(x + 114, y), Size = new Size(250, 20), BorderStyle = BorderStyle.Fixed3D };
        page.Controls.Add(tb);
    }

    private void BtnBrowseMusicPath_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select Music Folder" };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtMusicPath.Text = dlg.SelectedPath;
    }
}
