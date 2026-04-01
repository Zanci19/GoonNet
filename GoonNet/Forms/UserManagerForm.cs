using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class UserManagerForm : Form
{
    public UserDatabase UserDb { get; set; } = null!;

    private ListView _lvUsers = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnToggle = null!;
    private Button _btnResetPwd = null!;

    public UserManagerForm()
    {
        InitializeComponent();
        Load += UserManagerForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "User Manager";
        Size = new Size(640, 400);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 30 };

        _btnAdd = new Button { Text = "Add...", Location = new Point(4, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;
        _btnEdit = new Button { Text = "Edit...", Location = new Point(68, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;
        _btnDelete = new Button { Text = "Delete", Location = new Point(132, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;
        _btnToggle = new Button { Text = "Enable/Disable", Location = new Point(196, 3), Size = new Size(100, 22), FlatStyle = FlatStyle.System };
        _btnToggle.Click += BtnToggle_Click;
        _btnResetPwd = new Button { Text = "Reset Password", Location = new Point(300, 3), Size = new Size(110, 22), FlatStyle = FlatStyle.System };
        _btnResetPwd.Click += BtnResetPwd_Click;

        toolPanel.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete, _btnToggle, _btnResetPwd });

        _lvUsers = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvUsers.Columns.Add("Username", 110);
        _lvUsers.Columns.Add("Full Name", 140);
        _lvUsers.Columns.Add("Role", 80);
        _lvUsers.Columns.Add("Active", 60);
        _lvUsers.Columns.Add("Last Login", 110);
        _lvUsers.Columns.Add("Email", 130);
        _lvUsers.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(toolPanel);
        Controls.Add(_lvUsers);
    }

    private void UserManagerForm_Load(object? sender, EventArgs e) => RefreshList();

    private void RefreshList()
    {
        _lvUsers.BeginUpdate();
        _lvUsers.Items.Clear();
        foreach (var u in UserDb?.GetAll() ?? Enumerable.Empty<UserAccount>())
        {
            var lvi = new ListViewItem(u.Username);
            lvi.SubItems.Add(u.FullName);
            lvi.SubItems.Add(u.Role.ToString());
            lvi.SubItems.Add(u.IsActive ? "Yes" : "No");
            lvi.SubItems.Add(u.LastLogin?.ToString("dd/MM/yy HH:mm") ?? "Never");
            lvi.SubItems.Add(u.Email);
            lvi.Tag = u;
            if (!u.IsActive) lvi.ForeColor = SystemColors.GrayText;
            _lvUsers.Items.Add(lvi);
        }
        _lvUsers.EndUpdate();
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new UserEditorForm();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.User != null)
        {
            UserDb?.Add(dlg.User);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_lvUsers.SelectedItems.Count == 0) return;
        var user = (UserAccount?)_lvUsers.SelectedItems[0].Tag;
        if (user == null) return;
        using var dlg = new UserEditorForm(user);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            UserDb?.Update(user);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_lvUsers.SelectedItems.Count == 0) return;
        var user = (UserAccount?)_lvUsers.SelectedItems[0].Tag;
        if (user == null) return;
        if (MessageBox.Show($"Delete user '{user.Username}'?", "GoonNet",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            UserDb?.Delete(user.Id);
            RefreshList();
        }
    }

    private void BtnToggle_Click(object? sender, EventArgs e)
    {
        if (_lvUsers.SelectedItems.Count == 0) return;
        var user = (UserAccount?)_lvUsers.SelectedItems[0].Tag;
        if (user == null) return;
        user.IsActive = !user.IsActive;
        UserDb?.Update(user);
        RefreshList();
    }

    private void BtnResetPwd_Click(object? sender, EventArgs e)
    {
        if (_lvUsers.SelectedItems.Count == 0) return;
        var user = (UserAccount?)_lvUsers.SelectedItems[0].Tag;
        if (user == null) return;
        var newPwd = InputBoxForm.Show($"New password for '{user.Username}':", "Reset Password", owner: this);
        if (string.IsNullOrWhiteSpace(newPwd)) return;
        UserDb?.ChangePassword(user.Id, newPwd);
        MessageBox.Show("Password reset successfully.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
