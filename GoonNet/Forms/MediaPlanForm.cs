using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Table-driven spots scheduling (media plan) for full control of spot scheduling.
/// Shows a 7-day × 24-hour grid with scheduled ad slots.
/// </summary>
public class MediaPlanForm : Form
{
    public AdDatabase AdDb { get; set; } = null!;
    public SpotScheduleDatabase SpotDb { get; set; } = null!;
    public EconomicCategoryDatabase CategoryDb { get; set; } = null!;

    private DataGridView _grid = null!;
    private ComboBox _cboDayFilter = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnSave = null!;
    private Label _lblSummary = null!;

    public MediaPlanForm()
    {
        InitializeComponent();
        Load += (s, e) => RefreshGrid();
    }

    private void InitializeComponent()
    {
        Text = "Media Plan – Spots Scheduling";
        Size = new Size(1100, 640);
        MinimumSize = new Size(800, 480);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        // ── Toolbar ─────────────────────────────────────────────────────────
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(235, 238, 250) };

        var lblDay = new Label { Text = "Day:", Location = new Point(6, 8), Size = new Size(30, 16) };
        _cboDayFilter = new ComboBox { Location = new Point(38, 5), Size = new Size(110, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        _cboDayFilter.Items.Add("All Days");
        foreach (DayOfWeek d in Enum.GetValues<DayOfWeek>()) _cboDayFilter.Items.Add(d.ToString());
        _cboDayFilter.SelectedIndex = 0;
        _cboDayFilter.SelectedIndexChanged += (s, e) => RefreshGrid();

        _btnAdd = new Button { Text = "+ Add Slot", Location = new Point(158, 4), Size = new Size(90, 24), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "✏ Edit", Location = new Point(254, 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "🗑 Delete", Location = new Point(330, 4), Size = new Size(80, 24), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _btnSave = new Button { Text = "💾 Save", Location = new Point(420, 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System };
        _btnSave.Click += async (s, e) =>
        {
            await SpotDb.SaveAsync();
            MessageBox.Show("Media plan saved.", "GoonNet");
        };

        _lblSummary = new Label { Location = new Point(510, 8), Size = new Size(400, 16), ForeColor = Color.FromArgb(60, 80, 140) };

        toolbar.Controls.AddRange(new Control[] { lblDay, _cboDayFilter, _btnAdd, _btnEdit, _btnDelete, _btnSave, _lblSummary });

        // ── Grid ────────────────────────────────────────────────────────────
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Font = new Font("Microsoft Sans Serif", 8f),
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day", Width = 90, DataPropertyName = "Day" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", Width = 60, DataPropertyName = "Time" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ad", Width = 240, DataPropertyName = "AdTitle" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Advertiser", Width = 160, DataPropertyName = "Advertiser" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Desired", Width = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Max", Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fixed", Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Active", Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", Width = 200 });
        _grid.CellDoubleClick += (s, e) => BtnEdit_Click(s, EventArgs.Empty);

        Controls.Add(_grid);
        Controls.Add(toolbar);
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        if (SpotDb == null) return;

        var entries = SpotDb.GetAll();

        if (_cboDayFilter.SelectedIndex > 0)
        {
            int dayIdx = _cboDayFilter.SelectedIndex - 1;
            entries = entries.Where(e => e.DayOfWeek == dayIdx).ToList();
        }

        entries = entries.OrderBy(e => e.DayOfWeek).ThenBy(e => e.Hour).ThenBy(e => e.Minute).ToList();

        foreach (var entry in entries)
        {
            var day = ((DayOfWeek)entry.DayOfWeek).ToString();
            var time = $"{entry.Hour:00}:{entry.Minute:00}";
            int rowIdx = _grid.Rows.Add(
                day, time, entry.AdTitle, entry.Advertiser,
                entry.DesiredPlays, entry.MaxPlays,
                entry.IsFixed ? "Yes" : "No",
                entry.IsActive ? "Yes" : "No",
                entry.Notes);

            _grid.Rows[rowIdx].Tag = entry.Id;
            if (!entry.IsActive) _grid.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.Gray;
            if (entry.IsFixed) _grid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 220);
        }

        int total = entries.Count;
        int active = entries.Count(e => e.IsActive);
        _lblSummary.Text = $"{total} slots  |  {active} active";
    }

    private Guid? SelectedEntryId()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        return _grid.SelectedRows[0].Tag as Guid?;
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var entry = new SpotScheduleEntry();
        using var dlg = new SpotSlotDialog(entry, AdDb, CategoryDb);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            SpotDb.Add(entry);
            RefreshGrid();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        var id = SelectedEntryId();
        if (id == null) return;
        var entry = SpotDb.GetById(id.Value);
        if (entry == null) return;
        using var dlg = new SpotSlotDialog(entry, AdDb, CategoryDb);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            SpotDb.Update(entry);
            RefreshGrid();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var id = SelectedEntryId();
        if (id == null) return;
        if (MessageBox.Show("Delete this spot slot?", "Media Plan", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            SpotDb.Delete(id.Value);
            RefreshGrid();
        }
    }
}

// ── Spot slot editor dialog ──────────────────────────────────────────────────

internal class SpotSlotDialog : Form
{
    private readonly SpotScheduleEntry _entry;
    private readonly AdDatabase? _adDb;
    private readonly EconomicCategoryDatabase? _catDb;

    private ComboBox _cboDow = null!;
    private NumericUpDown _nudHour = null!;
    private NumericUpDown _nudMin = null!;
    private ComboBox _cboAd = null!;
    private NumericUpDown _nudDesired = null!;
    private NumericUpDown _nudMax = null!;
    private CheckBox _chkFixed = null!;
    private CheckBox _chkActive = null!;
    private TextBox _txtNotes = null!;

    public SpotSlotDialog(SpotScheduleEntry entry, AdDatabase? adDb, EconomicCategoryDatabase? catDb)
    {
        _entry = entry;
        _adDb = adDb;
        _catDb = catDb;
        InitializeComponent();
        LoadFields();
    }

    private void InitializeComponent()
    {
        Text = "Spot Slot";
        Size = new Size(440, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;

        int y = 14, lx = 10, lw = 90, fx = 106, fw = 300;

        void Row(string label, Control ctrl)
        {
            Controls.Add(new Label { Text = label, Location = new Point(lx, y + 2), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
            ctrl.Location = new Point(fx, y); ctrl.Width = fw;
            Controls.Add(ctrl); y += 28;
        }

        _cboDow = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (DayOfWeek d in Enum.GetValues<DayOfWeek>()) _cboDow.Items.Add(d.ToString());
        _cboDow.SelectedIndex = 0;
        Row("Day:", _cboDow);

        Controls.Add(new Label { Text = "Time (HH:MM):", Location = new Point(lx, y + 2), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
        _nudHour = new NumericUpDown { Location = new Point(fx, y), Size = new Size(56, 20), Minimum = 0, Maximum = 23 };
        _nudMin = new NumericUpDown { Location = new Point(fx + 64, y), Size = new Size(56, 20), Minimum = 0, Maximum = 59 };
        Controls.Add(_nudHour); Controls.Add(_nudMin);
        y += 28;

        _cboAd = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        if (_adDb != null)
            foreach (var ad in _adDb.GetAll()) _cboAd.Items.Add(ad);
        _cboAd.DisplayMember = "Title";
        _cboAd.SelectedIndex = _cboAd.Items.Count > 0 ? 0 : -1;
        Row("Ad:", _cboAd);

        Controls.Add(new Label { Text = "Desired/Max:", Location = new Point(lx, y + 2), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
        _nudDesired = new NumericUpDown { Location = new Point(fx, y), Size = new Size(56, 20), Minimum = 0, Maximum = 100, Value = 1 };
        _nudMax = new NumericUpDown { Location = new Point(fx + 66, y), Size = new Size(56, 20), Minimum = 0, Maximum = 100, Value = 1 };
        Controls.Add(_nudDesired); Controls.Add(_nudMax);
        y += 28;

        _chkFixed = new CheckBox { Text = "Fixed slot (no reschedule)", Location = new Point(fx, y) };
        Controls.Add(_chkFixed); y += 26;

        _chkActive = new CheckBox { Text = "Active", Location = new Point(fx, y), Checked = true };
        Controls.Add(_chkActive); y += 26;

        _txtNotes = new TextBox { Multiline = true, Height = 42 };
        Row("Notes:", _txtNotes);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(fx, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        btnOk.Click += BtnOk_Click;
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(fx + 90, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        Controls.Add(btnOk); Controls.Add(btnCancel);
        AcceptButton = btnOk; CancelButton = btnCancel;
        ClientSize = new Size(420, y + 54);
    }

    private void LoadFields()
    {
        _cboDow.SelectedIndex = _entry.DayOfWeek;
        _nudHour.Value = _entry.Hour;
        _nudMin.Value = _entry.Minute;
        _nudDesired.Value = Math.Max(0, _entry.DesiredPlays);
        _nudMax.Value = Math.Max(0, _entry.MaxPlays);
        _chkFixed.Checked = _entry.IsFixed;
        _chkActive.Checked = _entry.IsActive;
        _txtNotes.Text = _entry.Notes;

        if (_adDb != null)
        {
            foreach (var item in _cboAd.Items)
                if (item is Advertisement ad && ad.Id == _entry.AdId) { _cboAd.SelectedItem = item; break; }
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        _entry.DayOfWeek = _cboDow.SelectedIndex;
        _entry.Hour = (int)_nudHour.Value;
        _entry.Minute = (int)_nudMin.Value;
        _entry.DesiredPlays = (int)_nudDesired.Value;
        _entry.MaxPlays = (int)_nudMax.Value;
        _entry.IsFixed = _chkFixed.Checked;
        _entry.IsActive = _chkActive.Checked;
        _entry.Notes = _txtNotes.Text.Trim();
        if (_cboAd.SelectedItem is Advertisement selected)
        {
            _entry.AdId = selected.Id;
            _entry.AdTitle = selected.Title;
            _entry.Advertiser = selected.Advertiser;
        }
    }
}
