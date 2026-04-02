using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public class BlockManagerForm : Form
{
    public BlockDatabase BlockDb { get; set; } = null!;

    private ListView _lvBlocks = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Label _lblCount = null!;

    public BlockManagerForm()
    {
        InitializeComponent();
        Load += (s, e) => RefreshList();
    }

    private void InitializeComponent()
    {
        Text = "Block Manager";
        Size = new Size(740, 500);
        MinimumSize = new Size(560, 380);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 30 };

        _btnAdd = new Button { Text = "Add...", Location = new Point(4, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "Edit...", Location = new Point(68, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Delete", Location = new Point(132, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _lblCount = new Label { Text = "0 blocks", Location = new Point(204, 6), Size = new Size(100, 16) };

        toolPanel.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete, _lblCount });

        _lvBlocks = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvBlocks.Columns.Add("Name", 200);
        _lvBlocks.Columns.Add("Type", 100);
        _lvBlocks.Columns.Add("Sched. Time", 90);
        _lvBlocks.Columns.Add("Items", 50);
        _lvBlocks.Columns.Add("Fade (s)", 60);
        _lvBlocks.Columns.Add("Comments", 220);
        _lvBlocks.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(_lvBlocks);
        Controls.Add(toolPanel);
    }

    private void RefreshList()
    {
        _lvBlocks.BeginUpdate();
        _lvBlocks.Items.Clear();
        int count = 0;
        foreach (var block in BlockDb.GetAll())
        {
            var lvi = new ListViewItem(block.Name);
            lvi.SubItems.Add(block.IsTimedBlock ? "Timed" : "Standard");
            lvi.SubItems.Add(block.ScheduledTime.HasValue ? block.ScheduledTime.Value.ToString(@"hh\:mm") : "");
            lvi.SubItems.Add(block.Items.Count.ToString());
            lvi.SubItems.Add(block.SoftFadeDuration.TotalSeconds.ToString("0"));
            lvi.SubItems.Add(block.Comments);
            lvi.Tag = block.Id;
            _lvBlocks.Items.Add(lvi);
            count++;
        }
        _lvBlocks.EndUpdate();
        if (_lblCount != null) _lblCount.Text = $"{count} block{(count == 1 ? "" : "s")}";
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var block = new Block { Name = "New Block" };
        using var dlg = new BlockEditorDialog(block);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            BlockDb.Add(block);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_lvBlocks.SelectedItems.Count == 0) return;
        var id = (Guid)_lvBlocks.SelectedItems[0].Tag!;
        var block = BlockDb.GetById(id);
        if (block == null) return;
        using var dlg = new BlockEditorDialog(block);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            BlockDb.Update(block);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_lvBlocks.SelectedItems.Count == 0) return;
        var id = (Guid)_lvBlocks.SelectedItems[0].Tag!;
        var block = BlockDb.GetById(id);
        if (block == null) return;
        if (MessageBox.Show($"Delete block '{block.Name}'?", "GoonNet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            BlockDb.Delete(id);
            RefreshList();
        }
    }
}

internal class BlockEditorDialog : Form
{
    private readonly Block _block;
    private TextBox _txtName = null!;
    private CheckBox _chkTimed = null!;
    private DateTimePicker _dtpScheduled = null!;
    private CheckBox _chkHasTime = null!;
    private NumericUpDown _nudFade = null!;
    private TextBox _txtComments = null!;

    public BlockEditorDialog(Block block)
    {
        _block = block;
        InitializeComponent();
        LoadFields();
    }

    private void InitializeComponent()
    {
        Text = "Edit Block";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 16;
        int lw = 110, fx = 128, fw = 320, lx = 10;

        void AddRow(string label, Control ctrl, int? cw = null)
        {
            Controls.Add(new Label { Text = label, Location = new Point(lx, y + 3), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
            ctrl.Location = new Point(fx, y);
            ctrl.Width = cw ?? fw;
            Controls.Add(ctrl);
            y += 28;
        }

        _txtName = new TextBox { BorderStyle = BorderStyle.Fixed3D };
        AddRow("Name:", _txtName);

        _chkTimed = new CheckBox { Text = "Timed Block (has scheduled time)", Location = new Point(fx, y) };
        _chkTimed.CheckedChanged += (s, e) =>
        {
            _chkHasTime.Enabled = _chkTimed.Checked;
            _dtpScheduled.Enabled = _chkTimed.Checked && _chkHasTime.Checked;
        };
        Controls.Add(_chkTimed);
        y += 28;

        _chkHasTime = new CheckBox { Text = "Set scheduled time:", Location = new Point(fx, y), Enabled = false };
        _chkHasTime.CheckedChanged += (s, e) => _dtpScheduled.Enabled = _chkHasTime.Checked;
        Controls.Add(_chkHasTime);
        y += 24;

        _dtpScheduled = new DateTimePicker
        {
            Location = new Point(fx + 16, y),
            Width = 150,
            Format = DateTimePickerFormat.Time,
            ShowUpDown = true,
            Enabled = false
        };
        Controls.Add(_dtpScheduled);
        y += 30;

        _nudFade = new NumericUpDown { Minimum = 0, Maximum = 60, Value = 3, DecimalPlaces = 1, Increment = 0.5m, BorderStyle = BorderStyle.Fixed3D };
        AddRow("Soft Fade (s):", _nudFade, 70);

        _txtComments = new TextBox { BorderStyle = BorderStyle.Fixed3D, Multiline = true, Height = 56, ScrollBars = ScrollBars.Vertical };
        AddRow("Comments:", _txtComments);
        y += 30;

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
        _txtName.Text = _block.Name;
        _chkTimed.Checked = _block.IsTimedBlock;
        if (_block.ScheduledTime.HasValue)
        {
            _chkHasTime.Checked = true;
            _dtpScheduled.Value = DateTime.Today + _block.ScheduledTime.Value;
        }
        _nudFade.Value = (decimal)Math.Clamp(_block.SoftFadeDuration.TotalSeconds, 0, 60);
        _txtComments.Text = _block.Comments;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Block name is required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        _block.Name = _txtName.Text.Trim();
        _block.IsTimedBlock = _chkTimed.Checked;
        _block.ScheduledTime = (_chkTimed.Checked && _chkHasTime.Checked)
            ? _dtpScheduled.Value.TimeOfDay
            : (TimeSpan?)null;
        _block.SoftFadeDuration = TimeSpan.FromSeconds((double)_nudFade.Value);
        _block.Comments = _txtComments.Text.Trim();
    }
}
