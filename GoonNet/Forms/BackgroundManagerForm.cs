using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

public class BackgroundManagerForm : Form
{
    public BackgroundDatabase BackgroundDb { get; set; } = null!;

    private ListView _lvBackgrounds = null!;
    private TextBox _txtSearch = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnPreview = null!;
    private Button _btnStop = null!;
    private Label _lblCount = null!;

    public BackgroundManagerForm()
    {
        InitializeComponent();
        Load += (s, e) => RefreshList();
    }

    private void InitializeComponent()
    {
        Text = "Background Manager";
        Size = new Size(860, 520);
        MinimumSize = new Size(640, 400);
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

        _lblCount = new Label { Text = "0 backgrounds", Location = new Point(604, 6), Size = new Size(130, 16) };

        toolPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, _btnAdd, _btnEdit, _btnDelete, _btnPreview, _btnStop, _lblCount });

        _lvBackgrounds = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvBackgrounds.Columns.Add("Title", 180);
        _lvBackgrounds.Columns.Add("Category", 100);
        _lvBackgrounds.Columns.Add("Duration", 65);
        _lvBackgrounds.Columns.Add("Vol %", 48);
        _lvBackgrounds.Columns.Add("Decay", 48);
        _lvBackgrounds.Columns.Add("Sustain", 52);
        _lvBackgrounds.Columns.Add("Release", 52);
        _lvBackgrounds.Columns.Add("Loop", 44);
        _lvBackgrounds.Columns.Add("Active", 44);
        _lvBackgrounds.Columns.Add("File", 180);
        _lvBackgrounds.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(_lvBackgrounds);
        Controls.Add(toolPanel);
    }

    private void RefreshList()
    {
        _lvBackgrounds.BeginUpdate();
        _lvBackgrounds.Items.Clear();
        string search = _txtSearch?.Text.Trim() ?? string.Empty;
        int count = 0;
        foreach (var bg in BackgroundDb.GetAll())
        {
            if (!string.IsNullOrEmpty(search) &&
                !bg.Title.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !bg.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var lvi = new ListViewItem(bg.Title);
            lvi.SubItems.Add(bg.Category);
            lvi.SubItems.Add(bg.Duration > TimeSpan.Zero ? bg.Duration.ToString(@"m\:ss") : "");
            lvi.SubItems.Add(bg.InitialVolume.ToString("0"));
            lvi.SubItems.Add(bg.Decay.ToString("0.0"));
            lvi.SubItems.Add(bg.Sustain.ToString("0.0"));
            lvi.SubItems.Add(bg.Release.ToString("0.0"));
            lvi.SubItems.Add(bg.IsLooping ? "Yes" : "No");
            lvi.SubItems.Add(bg.IsActive ? "Yes" : "No");
            lvi.SubItems.Add(bg.FileName);
            lvi.Tag = bg.Id;
            if (!bg.IsActive) lvi.ForeColor = SystemColors.GrayText;
            _lvBackgrounds.Items.Add(lvi);
            count++;
        }
        _lvBackgrounds.EndUpdate();
        if (_lblCount != null) _lblCount.Text = $"{count} background{(count == 1 ? "" : "s")}";
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var bg = new Background();
        using var dlg = new BackgroundEditorDialog(bg);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            BackgroundDb.Add(bg);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_lvBackgrounds.SelectedItems.Count == 0) return;
        var id = (Guid)_lvBackgrounds.SelectedItems[0].Tag!;
        var bg = BackgroundDb.GetById(id);
        if (bg == null) return;
        using var dlg = new BackgroundEditorDialog(bg);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            BackgroundDb.Update(bg);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_lvBackgrounds.SelectedItems.Count == 0) return;
        var id = (Guid)_lvBackgrounds.SelectedItems[0].Tag!;
        var bg = BackgroundDb.GetById(id);
        if (bg == null) return;
        if (MessageBox.Show($"Delete background '{bg.Title}'?", "GoonNet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            BackgroundDb.Delete(id);
            RefreshList();
        }
    }

    private void BtnPreview_Click(object? sender, EventArgs e)
    {
        if (_lvBackgrounds.SelectedItems.Count == 0) return;
        var id = (Guid)_lvBackgrounds.SelectedItems[0].Tag!;
        var bg = BackgroundDb.GetById(id);
        if (bg == null) return;
        string path = Path.Combine(bg.Location, bg.FileName);
        if (!File.Exists(path))
        {
            MessageBox.Show($"File not found:\n{path}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var track = new MusicTrack
        {
            Artist = "Background",
            Title = bg.Title,
            FileName = bg.FileName,
            Location = bg.Location
        };
        AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Preview);
    }
}

internal class BackgroundEditorDialog : Form
{
    private readonly Background _bg;
    private TextBox _txtTitle = null!;
    private TextBox _txtFile = null!;
    private TextBox _txtLocation = null!;
    private TextBox _txtCategory = null!;
    private NumericUpDown _nudVolume = null!;
    private NumericUpDown _nudDecay = null!;
    private NumericUpDown _nudSustain = null!;
    private NumericUpDown _nudRelease = null!;
    private CheckBox _chkLoop = null!;
    private CheckBox _chkActive = null!;

    public BackgroundEditorDialog(Background bg)
    {
        _bg = bg;
        InitializeComponent();
        LoadFields();
    }

    private void InitializeComponent()
    {
        Text = "Edit Background";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 16;
        int lw = 100, fx = 118, fw = 340, lx = 10;

        void AddRow(string label, Control ctrl, int? cw = null)
        {
            Controls.Add(new Label { Text = label, Location = new Point(lx, y + 3), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
            ctrl.Location = new Point(fx, y);
            ctrl.Width = cw ?? fw;
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

        _nudVolume = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 50, DecimalPlaces = 0, BorderStyle = BorderStyle.Fixed3D };
        AddRow("Volume %:", _nudVolume, 70);

        _nudDecay = new NumericUpDown { Minimum = 0, Maximum = 30, Value = 1, DecimalPlaces = 1, Increment = 0.1m, BorderStyle = BorderStyle.Fixed3D };
        AddRow("Decay (s):", _nudDecay, 70);

        _nudSustain = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 50, DecimalPlaces = 1, Increment = 1m, BorderStyle = BorderStyle.Fixed3D };
        AddRow("Sustain %:", _nudSustain, 70);

        _nudRelease = new NumericUpDown { Minimum = 0, Maximum = 30, Value = 1, DecimalPlaces = 1, Increment = 0.1m, BorderStyle = BorderStyle.Fixed3D };
        AddRow("Release (s):", _nudRelease, 70);

        _chkLoop = new CheckBox { Text = "Loop", Location = new Point(fx, y), Checked = true };
        Controls.Add(_chkLoop);
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
        ClientSize = new Size(470, y + 50);
    }

    private void LoadFields()
    {
        _txtTitle.Text = _bg.Title;
        _txtFile.Text = _bg.FileName;
        _txtLocation.Text = _bg.Location;
        _txtCategory.Text = _bg.Category;
        _nudVolume.Value = (decimal)Math.Clamp(_bg.InitialVolume, 0, 100);
        _nudDecay.Value = (decimal)Math.Clamp(_bg.Decay, 0, 30);
        _nudSustain.Value = (decimal)Math.Clamp(_bg.Sustain, 0, 100);
        _nudRelease.Value = (decimal)Math.Clamp(_bg.Release, 0, 30);
        _chkLoop.Checked = _bg.IsLooping;
        _chkActive.Checked = _bg.IsActive;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Background Audio File",
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
        _bg.Title = _txtTitle.Text.Trim();
        _bg.FileName = _txtFile.Text.Trim();
        _bg.Location = _txtLocation.Text.Trim();
        _bg.Category = _txtCategory.Text.Trim();
        _bg.InitialVolume = (double)_nudVolume.Value;
        _bg.Decay = (double)_nudDecay.Value;
        _bg.Sustain = (double)_nudSustain.Value;
        _bg.Release = (double)_nudRelease.Value;
        _bg.IsLooping = _chkLoop.Checked;
        _bg.IsActive = _chkActive.Checked;
    }
}
