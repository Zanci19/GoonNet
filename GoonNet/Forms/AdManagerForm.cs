using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

public class AdManagerForm : Form
{
    public AdDatabase AdDb { get; set; } = null!;

    private ListView _lvAds = null!;
    private TextBox _txtSearch = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnPreview = null!;
    private Button _btnStop = null!;
    private Label _lblCount = null!;

    public AdManagerForm()
    {
        InitializeComponent();
        Load += (s, e) => RefreshList();
    }

    private void InitializeComponent()
    {
        Text = "Advertisement Manager";
        Size = new Size(900, 520);
        MinimumSize = new Size(700, 400);
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

        _lblCount = new Label { Text = "0 ads", Location = new Point(604, 6), Size = new Size(120, 16) };

        toolPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, _btnAdd, _btnEdit, _btnDelete, _btnPreview, _btnStop, _lblCount });

        _lvAds = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvAds.Columns.Add("Title", 160);
        _lvAds.Columns.Add("Advertiser", 130);
        _lvAds.Columns.Add("Duration", 65);
        _lvAds.Columns.Add("Start Date", 90);
        _lvAds.Columns.Add("End Date", 90);
        _lvAds.Columns.Add("Max/Day", 55);
        _lvAds.Columns.Add("Today", 45);
        _lvAds.Columns.Add("Total Plays", 65);
        _lvAds.Columns.Add("Active", 45);
        _lvAds.Columns.Add("File", 160);
        _lvAds.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(_lvAds);
        Controls.Add(toolPanel);
    }

    private void RefreshList()
    {
        _lvAds.BeginUpdate();
        _lvAds.Items.Clear();
        string search = _txtSearch?.Text.Trim() ?? string.Empty;
        int count = 0;
        foreach (var ad in AdDb.GetAll())
        {
            if (!string.IsNullOrEmpty(search) &&
                !ad.Title.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !ad.Advertiser.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var lvi = new ListViewItem(ad.Title);
            lvi.SubItems.Add(ad.Advertiser);
            lvi.SubItems.Add(ad.Duration > TimeSpan.Zero ? ad.Duration.ToString(@"m\:ss") : "");
            lvi.SubItems.Add(ad.StartDate.ToString("dd/MM/yyyy"));
            lvi.SubItems.Add(ad.EndDate.ToString("dd/MM/yyyy"));
            lvi.SubItems.Add(ad.MaxPlaysPerDay.ToString());
            lvi.SubItems.Add(ad.PlaysToday.ToString());
            lvi.SubItems.Add(ad.TotalPlays.ToString());
            lvi.SubItems.Add(ad.IsActive ? "Yes" : "No");
            lvi.SubItems.Add(ad.FileName);
            lvi.Tag = ad.Id;

            bool expired = ad.EndDate < DateTime.Today;
            if (!ad.IsActive || expired)
                lvi.ForeColor = SystemColors.GrayText;
            else if (ad.StartDate > DateTime.Today)
                lvi.ForeColor = Color.DarkOrange;

            _lvAds.Items.Add(lvi);
            count++;
        }
        _lvAds.EndUpdate();
        if (_lblCount != null) _lblCount.Text = $"{count} advertisement{(count == 1 ? "" : "s")}";
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var ad = new Advertisement();
        using var dlg = new AdEditorDialog(ad);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            AdDb.Add(ad);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_lvAds.SelectedItems.Count == 0) return;
        var id = (Guid)_lvAds.SelectedItems[0].Tag!;
        var ad = AdDb.GetById(id);
        if (ad == null) return;
        using var dlg = new AdEditorDialog(ad);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            AdDb.Update(ad);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_lvAds.SelectedItems.Count == 0) return;
        var id = (Guid)_lvAds.SelectedItems[0].Tag!;
        var ad = AdDb.GetById(id);
        if (ad == null) return;
        if (MessageBox.Show($"Delete advertisement '{ad.Title}'?", "GoonNet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            AdDb.Delete(id);
            RefreshList();
        }
    }

    private void BtnPreview_Click(object? sender, EventArgs e)
    {
        if (_lvAds.SelectedItems.Count == 0) return;
        var id = (Guid)_lvAds.SelectedItems[0].Tag!;
        var ad = AdDb.GetById(id);
        if (ad == null) return;
        string path = Path.Combine(ad.Location, ad.FileName);
        if (!File.Exists(path))
        {
            MessageBox.Show($"File not found:\n{path}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var track = new MusicTrack
        {
            Artist = ad.Advertiser,
            Title = ad.Title,
            FileName = ad.FileName,
            Location = ad.Location
        };
        AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Preview);
    }
}

internal class AdEditorDialog : Form
{
    private readonly Advertisement _ad;
    private TextBox _txtTitle = null!;
    private TextBox _txtAdvertiser = null!;
    private TextBox _txtFile = null!;
    private TextBox _txtLocation = null!;
    private TextBox _txtContract = null!;
    private TextBox _txtComments = null!;
    private DateTimePicker _dtpStart = null!;
    private DateTimePicker _dtpEnd = null!;
    private NumericUpDown _nudMaxPerDay = null!;
    private NumericUpDown _nudPriority = null!;
    private CheckBox _chkActive = null!;

    public AdEditorDialog(Advertisement ad)
    {
        _ad = ad;
        InitializeComponent();
        LoadFields();
    }

    private void InitializeComponent()
    {
        Text = "Edit Advertisement";
        Size = new Size(520, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 16;
        int lw = 110, fx = 128, fw = 340, lx = 10;

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

        _txtAdvertiser = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        AddRow("Advertiser:", _txtAdvertiser);

        _txtFile = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        var btnBrowse = new Button { Text = "...", Location = new Point(fx + fw + 4, y - 28), Size = new Size(28, 22), FlatStyle = FlatStyle.System };
        btnBrowse.Click += BtnBrowse_Click;
        Controls.Add(btnBrowse);
        AddRow("File:", _txtFile);

        _txtLocation = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        AddRow("Folder:", _txtLocation);

        _dtpStart = new DateTimePicker { Format = DateTimePickerFormat.Short };
        AddRow("Start Date:", _dtpStart, 120);

        _dtpEnd = new DateTimePicker { Format = DateTimePickerFormat.Short };
        AddRow("End Date:", _dtpEnd, 120);

        _nudMaxPerDay = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 3, BorderStyle = BorderStyle.Fixed3D };
        AddRow("Max Plays/Day:", _nudMaxPerDay, 60);

        _nudPriority = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 5, BorderStyle = BorderStyle.Fixed3D };
        AddRow("Priority:", _nudPriority, 60);

        _txtContract = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        AddRow("Contract #:", _txtContract);

        _txtComments = new TextBox { BorderStyle = BorderStyle.Fixed3D, Multiline = true, Height = 40, ScrollBars = ScrollBars.Vertical };
        AddRow("Comments:", _txtComments);
        y += 14; // extra height for multiline

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
        _txtTitle.Text = _ad.Title;
        _txtAdvertiser.Text = _ad.Advertiser;
        _txtFile.Text = _ad.FileName;
        _txtLocation.Text = _ad.Location;
        _txtContract.Text = _ad.ContractNumber;
        _txtComments.Text = _ad.Comments;
        _dtpStart.Value = _ad.StartDate > DateTime.MinValue ? _ad.StartDate : DateTime.Today;
        _dtpEnd.Value = _ad.EndDate > DateTime.MinValue ? _ad.EndDate : DateTime.Today.AddMonths(1);
        _nudMaxPerDay.Value = _ad.MaxPlaysPerDay;
        _nudPriority.Value = Math.Clamp(_ad.Priority, 1, 10);
        _chkActive.Checked = _ad.IsActive;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Advertisement Audio File",
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
        _ad.Title = _txtTitle.Text.Trim();
        _ad.Advertiser = _txtAdvertiser.Text.Trim();
        _ad.FileName = _txtFile.Text.Trim();
        _ad.Location = _txtLocation.Text.Trim();
        _ad.ContractNumber = _txtContract.Text.Trim();
        _ad.Comments = _txtComments.Text.Trim();
        _ad.StartDate = _dtpStart.Value.Date;
        _ad.EndDate = _dtpEnd.Value.Date;
        _ad.MaxPlaysPerDay = (int)_nudMaxPerDay.Value;
        _ad.Priority = (int)_nudPriority.Value;
        _ad.IsActive = _chkActive.Checked;
    }
}
