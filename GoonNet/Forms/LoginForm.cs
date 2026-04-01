using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public class LoginForm : Form
{
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private Button _btnLogin = null!;
    private Button _btnCancel = null!;
    private Label _lblError = null!;

    public UserAccount? LoggedInUser { get; private set; }
    public UserDatabase UserDb { get; set; } = null!;

    public LoginForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "GoonNet - Login";
        Size = new Size(300, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var lblTitle = new Label
        {
            Text = "GoonNet Radio Automation",
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            Location = new Point(8, 8),
            Size = new Size(270, 16),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblUser = new Label { Text = "Username:", Location = new Point(16, 36), Size = new Size(70, 16), TextAlign = ContentAlignment.MiddleRight };
        _txtUsername = new TextBox { Location = new Point(90, 34), Size = new Size(180, 20), BorderStyle = BorderStyle.Fixed3D };

        var lblPass = new Label { Text = "Password:", Location = new Point(16, 62), Size = new Size(70, 16), TextAlign = ContentAlignment.MiddleRight };
        _txtPassword = new TextBox { Location = new Point(90, 60), Size = new Size(180, 20), PasswordChar = '*', BorderStyle = BorderStyle.Fixed3D };

        _lblError = new Label
        {
            Text = string.Empty,
            Location = new Point(16, 86),
            Size = new Size(260, 16),
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _btnLogin = new Button
        {
            Text = "Login",
            Location = new Point(110, 108),
            Size = new Size(70, 24),
            FlatStyle = FlatStyle.System
        };
        _btnLogin.Click += BtnLogin_Click;

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(190, 108),
            Size = new Size(70, 24),
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = _btnLogin;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[] { lblTitle, lblUser, _txtUsername, lblPass, _txtPassword, _lblError, _btnLogin, _btnCancel });
    }

    private void BtnLogin_Click(object? sender, EventArgs e)
    {
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            _lblError.Text = "Please enter a username.";
            return;
        }

        // Allow default admin/admin if no users exist yet
        if (UserDb == null || UserDb.GetAll().Count == 0)
        {
            if (username == "admin" && password == "admin")
            {
                LoggedInUser = new UserAccount
                {
                    Username = "admin",
                    FullName = "Administrator",
                    Role = UserRole.Admin,
                    CanEditMusic = true,
                    CanEditSchedule = true,
                    CanManageUsers = true,
                    CanViewLogs = true
                };
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
        }

        var user = UserDb?.Authenticate(username, password);
        if (user != null)
        {
            LoggedInUser = user;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblError.Text = "Invalid username or password.";
            _txtPassword.Clear();
            _txtPassword.Focus();
        }
    }
}
