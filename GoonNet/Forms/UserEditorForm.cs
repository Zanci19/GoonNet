using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Dialog for adding/editing a UserAccount.
/// </summary>
public class UserEditorForm : Form
{
    public UserAccount? User { get; private set; }

    private TextBox _txtUsername = null!;
    private TextBox _txtFullName = null!;
    private TextBox _txtEmail = null!;
    private TextBox _txtPassword = null!;
    private ComboBox _cmbRole = null!;
    private CheckBox _chkActive = null!;
    private CheckBox _chkEditMusic = null!;
    private CheckBox _chkEditSchedule = null!;
    private CheckBox _chkManageUsers = null!;
    private CheckBox _chkViewLogs = null!;
    private Button _btnOK = null!;
    private Button _btnCancel = null!;
    private readonly bool _isNew;

    public UserEditorForm() : this(null) { }

    public UserEditorForm(UserAccount? user)
    {
        _isNew = user == null;
        User = user ?? new UserAccount();
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        Text = _isNew ? "Add User" : "Edit User";
        Size = new Size(360, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        int lx = 8, tx = 110, tw = 220, row = 8, rh = 26;

        Label L(string t) => new Label { Text = t, Location = new Point(lx, row + 3), Size = new Size(100, 16), TextAlign = ContentAlignment.MiddleRight };
        TextBox T() => new TextBox { Location = new Point(tx, row), Size = new Size(tw, 20), BorderStyle = BorderStyle.Fixed3D };

        var lblUser = L("Username:"); _txtUsername = T(); row += rh;
        var lblFull = L("Full Name:"); _txtFullName = T(); row += rh;
        var lblEmail = L("Email:"); _txtEmail = T(); row += rh;
        var lblPwd = L(_isNew ? "Password:" : "New Password:"); _txtPassword = T(); _txtPassword.PasswordChar = '*'; row += rh;

        var lblRole = L("Role:");
        _cmbRole = new ComboBox { Location = new Point(tx, row), Size = new Size(150, 20), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        foreach (var v in Enum.GetValues<UserRole>()) _cmbRole.Items.Add(v);
        _cmbRole.SelectedIndex = 2;
        row += rh;

        _chkActive = new CheckBox { Text = "Active", Location = new Point(tx, row), Size = new Size(80, 18), Checked = true }; row += rh;

        var lblPerms = new Label { Text = "Permissions:", Location = new Point(lx, row + 2), Size = new Size(100, 16), TextAlign = ContentAlignment.MiddleRight };
        _chkEditMusic = new CheckBox { Text = "Edit Music", Location = new Point(tx, row), Size = new Size(100, 18) };
        row += rh;
        _chkEditSchedule = new CheckBox { Text = "Edit Schedule", Location = new Point(tx, row), Size = new Size(110, 18) };
        row += rh;
        _chkManageUsers = new CheckBox { Text = "Manage Users", Location = new Point(tx, row), Size = new Size(110, 18) };
        row += rh;
        _chkViewLogs = new CheckBox { Text = "View Logs", Location = new Point(tx, row), Size = new Size(100, 18), Checked = true };
        row += rh;

        _btnOK = new Button { Text = "OK", Location = new Point(tx + 80, row + 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnOK.Click += BtnOK_Click;
        _btnCancel = new Button { Text = "Cancel", Location = new Point(tx + 158, row + 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            lblUser, _txtUsername, lblFull, _txtFullName, lblEmail, _txtEmail,
            lblPwd, _txtPassword, lblRole, _cmbRole, _chkActive,
            lblPerms, _chkEditMusic, _chkEditSchedule, _chkManageUsers, _chkViewLogs,
            _btnOK, _btnCancel
        });

        ClientSize = new Size(344, row + 36);
    }

    private void PopulateFields()
    {
        if (User == null) return;
        _txtUsername.Text = User.Username;
        _txtFullName.Text = User.FullName;
        _txtEmail.Text = User.Email;
        _cmbRole.SelectedItem = User.Role;
        _chkActive.Checked = User.IsActive;
        _chkEditMusic.Checked = User.CanEditMusic;
        _chkEditSchedule.Checked = User.CanEditSchedule;
        _chkManageUsers.Checked = User.CanManageUsers;
        _chkViewLogs.Checked = User.CanViewLogs;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtUsername.Text))
        {
            MessageBox.Show("Username is required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        if (_isNew && string.IsNullOrWhiteSpace(_txtPassword.Text))
        {
            MessageBox.Show("Password is required for new users.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        User ??= new UserAccount();
        User.Username = _txtUsername.Text.Trim();
        User.FullName = _txtFullName.Text.Trim();
        User.Email = _txtEmail.Text.Trim();
        User.Role = (UserRole)(_cmbRole.SelectedItem ?? UserRole.ReadOnly);
        User.IsActive = _chkActive.Checked;
        User.CanEditMusic = _chkEditMusic.Checked;
        User.CanEditSchedule = _chkEditSchedule.Checked;
        User.CanManageUsers = _chkManageUsers.Checked;
        User.CanViewLogs = _chkViewLogs.Checked;

        if (!string.IsNullOrWhiteSpace(_txtPassword.Text))
            User.PasswordHash = UserDatabase.HashPassword(_txtPassword.Text);
    }
}
