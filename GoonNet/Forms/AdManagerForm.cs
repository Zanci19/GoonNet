using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

public class AdManagerForm : Form
{
    public AdDatabase AdDb { get; set; } = null!;

    private ListView _lvAds = null!;
    private ListView _lvPlayQueue = null!;
    private TextBox _txtSearch = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnPreview = null!;
    private Button _btnStop = null!;
    private Button _btnQueue = null!;
    private Button _btnPlayQueued = null!;
    private Label _lblCount = null!;
    private readonly List<Guid> _queuedIds = new();

    public AdManagerForm()
    {
        InitializeComponent();
        Load += (s, e) => RefreshList();
    }

    private void InitializeComponent()
    {
        Text = "Advertisement Desk";
        Size = new Size(930, 560);
        MinimumSize = new Size(760, 480);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 34 };

        var lblSearch = new Label { Text = "Find:", Location = new Point(6, 10), Size = new Size(32, 16) };
        _txtSearch = new TextBox { Location = new Point(40, 7), Size = new Size(200, 20), BorderStyle = BorderStyle.Fixed3D };
        _txtSearch.TextChanged += (s, e) => RefreshList();

        _btnAdd = new Button { Text = "Add", Location = new Point(246, 6), Size = new Size(52, 22), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "Edit", Location = new Point(302, 6), Size = new Size(52, 22), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Delete", Location = new Point(358, 6), Size = new Size(56, 22), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _btnPreview = new Button { Text = "▶ Preview", Location = new Point(420, 6), Size = new Size(74, 22), FlatStyle = FlatStyle.System };
        _btnPreview.Click += BtnPreview_Click;

        _btnStop = new Button { Text = "■ Stop", Location = new Point(498, 6), Size = new Size(56, 22), FlatStyle = FlatStyle.System };
        _btnStop.Click += (s, e) => AudioEngine.Instance.Stop(AudioDeviceType.Preview);

        _btnQueue = new Button { Text = "Queue +", Location = new Point(560, 6), Size = new Size(66, 22), FlatStyle = FlatStyle.System };
        _btnQueue.Click += BtnQueue_Click;

        _btnPlayQueued = new Button { Text = "Play Queue", Location = new Point(630, 6), Size = new Size(78, 22), FlatStyle = FlatStyle.System };
        _btnPlayQueued.Click += BtnPlayQueued_Click;

        _lblCount = new Label { Text = "0 ads", Location = new Point(720, 10), Size = new Size(180, 16) };

        topPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, _btnAdd, _btnEdit, _btnDelete, _btnPreview, _btnStop, _btnQueue, _btnPlayQueued, _lblCount });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 325,
            SplitterWidth = 4,
            BorderStyle = BorderStyle.Fixed3D
        };

        _lvAds = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvAds.Columns.Add("ID", 40);
        _lvAds.Columns.Add("Advertiser", 130);
        _lvAds.Columns.Add("Title", 190);
        _lvAds.Columns.Add("Category", 80);
        _lvAds.Columns.Add("Dur", 45);
        _lvAds.Columns.Add("Start", 70);
        _lvAds.Columns.Add("End", 70);
        _lvAds.Columns.Add("Max", 42);
        _lvAds.Columns.Add("Today", 45);
        _lvAds.Columns.Add("Status", 60);
        _lvAds.Columns.Add("File", 200);
        _lvAds.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        var queueHeader = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = SystemColors.ControlDark };
        queueHeader.Controls.Add(new Label
        {
            Text = "Ad Play Queue",
            ForeColor = Color.White,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            Location = new Point(6, 5),
            Size = new Size(120, 16)
        });

        _lvPlayQueue = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.Black,
            ForeColor = Color.FromArgb(190, 210, 255),
            Font = new Font("Consolas", 8f)
        };
        _lvPlayQueue.Columns.Add("#", 36);
        _lvPlayQueue.Columns.Add("Advertiser", 170);
        _lvPlayQueue.Columns.Add("Spot", 220);
        _lvPlayQueue.Columns.Add("Length", 60);
        _lvPlayQueue.Columns.Add("File", 320);
        _lvPlayQueue.DoubleClick += (s, e) => BtnPlayQueued_Click(s, e);

        split.Panel1.Controls.Add(_lvAds);
        split.Panel2.Controls.Add(_lvPlayQueue);
        split.Panel2.Controls.Add(queueHeader);

        Controls.Add(split);
        Controls.Add(topPanel);
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

            count++;
            var lvi = new ListViewItem(count.ToString());
            lvi.SubItems.Add(ad.Advertiser);
            lvi.SubItems.Add(ad.Title);
            lvi.SubItems.Add(ad.ContractNumber);
            lvi.SubItems.Add(ad.Duration > TimeSpan.Zero ? ad.Duration.ToString(@"m\:ss") : "");
            lvi.SubItems.Add(ad.StartDate.ToString("dd/MM/yy"));
            lvi.SubItems.Add(ad.EndDate.ToString("dd/MM/yy"));
            lvi.SubItems.Add(ad.MaxPlaysPerDay.ToString());
            lvi.SubItems.Add(ad.PlaysToday.ToString());
            lvi.SubItems.Add(ad.IsActive ? "Active" : "Off");
            lvi.SubItems.Add(ad.FileName);
            lvi.Tag = ad.Id;

            bool expired = ad.EndDate < DateTime.Today;
            if (!ad.IsActive || expired)
                lvi.ForeColor = SystemColors.GrayText;
            else if (ad.StartDate > DateTime.Today)
                lvi.ForeColor = Color.DarkOrange;

            _lvAds.Items.Add(lvi);
        }
        _lvAds.EndUpdate();
        _lblCount.Text = $"{count} advertisement{(count == 1 ? "" : "s")}";
    }

    private void BtnQueue_Click(object? sender, EventArgs e)
    {
        if (_lvAds.SelectedItems.Count == 0) return;
        var id = (Guid)_lvAds.SelectedItems[0].Tag!;
        var ad = AdDb.GetById(id);
        if (ad == null) return;

        _queuedIds.Add(id);
        var item = new ListViewItem(_queuedIds.Count.ToString());
        item.SubItems.Add(ad.Advertiser);
        item.SubItems.Add(ad.Title);
        item.SubItems.Add(ad.Duration > TimeSpan.Zero ? ad.Duration.ToString(@"m\:ss") : "");
        item.SubItems.Add(ad.FileName);
        item.Tag = id;
        _lvPlayQueue.Items.Add(item);
    }

    private void BtnPlayQueued_Click(object? sender, EventArgs e)
    {
        if (_lvPlayQueue.Items.Count == 0) return;
        var item = _lvPlayQueue.SelectedItems.Count > 0 ? _lvPlayQueue.SelectedItems[0] : _lvPlayQueue.Items[0];
        var id = (Guid)item.Tag!;
        PlayAdById(id);
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
        PlayAdById(id);
    }

    private void PlayAdById(Guid id)
    {
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
        y += 14;

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
