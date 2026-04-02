using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

public class JingleManagerForm : Form
{
    public JingleDatabase JingleDb { get; set; } = null!;

    private ListView _lvJingles = null!;
    private TextBox _txtSearch = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnPreview = null!;
    private Button _btnStop = null!;
    private Label _lblCount = null!;

    public JingleManagerForm()
    {
        InitializeComponent();
        Load += (s, e) => RefreshList();
    }

    private void InitializeComponent()
    {
        Text = "Jingle Manager";
        Size = new Size(800, 520);
        MinimumSize = new Size(600, 400);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 30 };

        var lblSearch = new Label { Text = "Search:", Location = new Point(4, 6), Size = new Size(48, 16) };
        _txtSearch = new TextBox { Location = new Point(56, 4), Size = new Size(200, 20), BorderStyle = BorderStyle.Fixed3D };
        _txtSearch.TextChanged += (s, e) => RefreshList();

        _btnAdd = new Button { Text = "Add...", Location = new Point(264, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "Edit...", Location = new Point(328, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Delete", Location = new Point(392, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _btnPreview = new Button { Text = "▶ Preview", Location = new Point(460, 3), Size = new Size(70, 22), FlatStyle = FlatStyle.System };
        _btnPreview.Click += BtnPreview_Click;

        _btnStop = new Button { Text = "■ Stop", Location = new Point(534, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnStop.Click += (s, e) => AudioEngine.Instance.Stop(AudioDeviceType.Preview);

        _lblCount = new Label { Text = "0 jingles", Location = new Point(604, 6), Size = new Size(100, 16) };

        toolPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, _btnAdd, _btnEdit, _btnDelete, _btnPreview, _btnStop, _lblCount });

        _lvJingles = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvJingles.Columns.Add("Title", 200);
        _lvJingles.Columns.Add("Category", 100);
        _lvJingles.Columns.Add("Duration", 70);
        _lvJingles.Columns.Add("Priority", 55);
        _lvJingles.Columns.Add("Plays", 50);
        _lvJingles.Columns.Add("Last Played", 110);
        _lvJingles.Columns.Add("Active", 50);
        _lvJingles.Columns.Add("File", 200);
        _lvJingles.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(_lvJingles);
        Controls.Add(toolPanel);
    }

    private void RefreshList()
    {
        _lvJingles.BeginUpdate();
        _lvJingles.Items.Clear();
        string search = _txtSearch?.Text.Trim().ToLowerInvariant() ?? string.Empty;
        int count = 0;
        foreach (var j in JingleDb.GetAll())
        {
            if (!string.IsNullOrEmpty(search) &&
                !j.Title.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !j.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var lvi = new ListViewItem(j.Title);
            lvi.SubItems.Add(j.Category);
            lvi.SubItems.Add(j.Duration > TimeSpan.Zero ? j.Duration.ToString(@"m\:ss") : "");
            lvi.SubItems.Add(j.Priority.ToString());
            lvi.SubItems.Add(j.PlayCount.ToString());
            lvi.SubItems.Add(j.LastPlayed.HasValue ? j.LastPlayed.Value.ToString("dd/MM/yyyy HH:mm") : "Never");
            lvi.SubItems.Add(j.IsActive ? "Yes" : "No");
            lvi.SubItems.Add(j.FileName);
            lvi.Tag = j.Id;
            if (!j.IsActive) lvi.ForeColor = SystemColors.GrayText;
            _lvJingles.Items.Add(lvi);
            count++;
        }
        _lvJingles.EndUpdate();
        if (_lblCount != null) _lblCount.Text = $"{count} jingle{(count == 1 ? "" : "s")}";
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var jingle = new Jingle();
        using var dlg = new JingleEditorDialog(jingle);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            JingleDb.Add(jingle);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_lvJingles.SelectedItems.Count == 0) return;
        var id = (Guid)_lvJingles.SelectedItems[0].Tag!;
        var jingle = JingleDb.GetById(id);
        if (jingle == null) return;
        using var dlg = new JingleEditorDialog(jingle);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            JingleDb.Update(jingle);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_lvJingles.SelectedItems.Count == 0) return;
        var id = (Guid)_lvJingles.SelectedItems[0].Tag!;
        var jingle = JingleDb.GetById(id);
        if (jingle == null) return;
        if (MessageBox.Show($"Delete jingle '{jingle.Title}'?", "GoonNet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            JingleDb.Delete(id);
            RefreshList();
        }
    }

    private void BtnPreview_Click(object? sender, EventArgs e)
    {
        if (_lvJingles.SelectedItems.Count == 0) return;
        var id = (Guid)_lvJingles.SelectedItems[0].Tag!;
        var jingle = JingleDb.GetById(id);
        if (jingle == null) return;
        string path = Path.Combine(jingle.Location, jingle.FileName);
        if (!File.Exists(path))
        {
            MessageBox.Show($"File not found:\n{path}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var track = new MusicTrack
        {
            Artist = "Jingle",
            Title = jingle.Title,
            FileName = jingle.FileName,
            Location = jingle.Location
        };
        AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Preview);
    }
}

internal class JingleEditorDialog : Form
{
    private readonly Jingle _jingle;
    private TextBox _txtTitle = null!;
    private TextBox _txtFile = null!;
    private TextBox _txtLocation = null!;
    private TextBox _txtCategory = null!;
    private NumericUpDown _nudPriority = null!;
    private CheckBox _chkActive = null!;

    public JingleEditorDialog(Jingle jingle)
    {
        _jingle = jingle;
        InitializeComponent();
        LoadFields();
    }

    private void InitializeComponent()
    {
        Text = "Edit Jingle";
        Size = new Size(500, 270);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 16;
        int lw = 100, fw = 340, lx = 12, fx = 116;

        void AddRow(string label, Control ctrl)
        {
            Controls.Add(new Label { Text = label, Location = new Point(lx, y + 3), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
            ctrl.Location = new Point(fx, y);
            ctrl.Width = fw;
            Controls.Add(ctrl);
            y += 28;
        }

        _txtTitle = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        AddRow("Title:", _txtTitle);

        _txtFile = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        var btnBrowse = new Button { Text = "...", Location = new Point(fx + fw + 4, y - 28), Size = new Size(28, 22), FlatStyle = FlatStyle.System };
        btnBrowse.Click += BtnBrowse_Click;
        Controls.Add(btnBrowse);
        AddRow("File:", _txtFile);

        _txtLocation = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        AddRow("Folder:", _txtLocation);

        _txtCategory = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        AddRow("Category:", _txtCategory);

        _nudPriority = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 5, BorderStyle = BorderStyle.Fixed3D };
        _nudPriority.Width = 60;
        Controls.Add(new Label { Text = "Priority:", Location = new Point(lx, y + 3), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
        _nudPriority.Location = new Point(fx, y);
        Controls.Add(_nudPriority);
        y += 28;

        _chkActive = new CheckBox { Text = "Active", Location = new Point(fx, y), Checked = true };
        Controls.Add(_chkActive);
        y += 28;

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(fx, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        btnOk.Click += BtnOk_Click;
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(fx + 90, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ClientSize = new Size(460, y + 50);
    }

    private void LoadFields()
    {
        _txtTitle.Text = _jingle.Title;
        _txtFile.Text = _jingle.FileName;
        _txtLocation.Text = _jingle.Location;
        _txtCategory.Text = _jingle.Category;
        _nudPriority.Value = Math.Clamp(_jingle.Priority, 1, 10);
        _chkActive.Checked = _jingle.IsActive;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Jingle Audio File",
            Filter = "Audio Files|*.mp3;*.wav;*.ogg;*.aac;*.flac|All Files|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtFile.Text = Path.GetFileName(dlg.FileName);
            _txtLocation.Text = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            MessageBox.Show("Title is required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        _jingle.Title = _txtTitle.Text.Trim();
        _jingle.FileName = _txtFile.Text.Trim();
        _jingle.Location = _txtLocation.Text.Trim();
        _jingle.Category = _txtCategory.Text.Trim();
        _jingle.Priority = (int)_nudPriority.Value;
        _jingle.IsActive = _chkActive.Checked;
    }
}
