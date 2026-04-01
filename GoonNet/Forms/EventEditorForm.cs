using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Dialog for adding/editing a BroadcastEvent.
/// </summary>
public class EventEditorForm : Form
{
    public BroadcastEvent? BroadcastEvent { get; private set; }

    private TextBox _txtName = null!;
    private ComboBox _cmbType = null!;
    private DateTimePicker _dtpTime = null!;
    private CheckBox _chkMon = null!, _chkTue = null!, _chkWed = null!, _chkThu = null!, _chkFri = null!, _chkSat = null!, _chkSun = null!;
    private NumericUpDown _nudPriority = null!;
    private TextBox _txtComments = null!;
    private CheckBox _chkActive = null!;
    private Button _btnOK = null!;
    private Button _btnCancel = null!;

    public EventEditorForm() : this(null) { }

    public EventEditorForm(BroadcastEvent? ev)
    {
        BroadcastEvent = ev ?? new BroadcastEvent();
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        Text = "Event Editor";
        Size = new Size(380, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        int lx = 8, tx = 100, row = 8, rh = 26;

        var lblName = new Label { Text = "Name:", Location = new Point(lx, row + 3), Size = new Size(88, 16), TextAlign = ContentAlignment.MiddleRight };
        _txtName = new TextBox { Location = new Point(tx, row), Size = new Size(252, 20), BorderStyle = BorderStyle.Fixed3D };
        row += rh;

        var lblType = new Label { Text = "Type:", Location = new Point(lx, row + 3), Size = new Size(88, 16), TextAlign = ContentAlignment.MiddleRight };
        _cmbType = new ComboBox { Location = new Point(tx, row), Size = new Size(150, 20), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        foreach (var v in Enum.GetValues<EventType>()) _cmbType.Items.Add(v);
        _cmbType.SelectedIndex = 0;
        row += rh;

        var lblTime = new Label { Text = "Time:", Location = new Point(lx, row + 3), Size = new Size(88, 16), TextAlign = ContentAlignment.MiddleRight };
        _dtpTime = new DateTimePicker { Location = new Point(tx, row), Size = new Size(150, 20), Format = DateTimePickerFormat.Time, ShowUpDown = true };
        row += rh;

        var lblDays = new Label { Text = "Days:", Location = new Point(lx, row + 3), Size = new Size(88, 16), TextAlign = ContentAlignment.MiddleRight };
        _chkMon = new CheckBox { Text = "Mon", Location = new Point(tx, row), Size = new Size(44, 18) };
        _chkTue = new CheckBox { Text = "Tue", Location = new Point(tx + 44, row), Size = new Size(44, 18) };
        _chkWed = new CheckBox { Text = "Wed", Location = new Point(tx + 88, row), Size = new Size(44, 18) };
        _chkThu = new CheckBox { Text = "Thu", Location = new Point(tx + 132, row), Size = new Size(44, 18) };
        _chkFri = new CheckBox { Text = "Fri", Location = new Point(tx + 176, row), Size = new Size(40, 18) };
        row += rh;
        _chkSat = new CheckBox { Text = "Sat", Location = new Point(tx, row), Size = new Size(44, 18) };
        _chkSun = new CheckBox { Text = "Sun", Location = new Point(tx + 44, row), Size = new Size(44, 18) };
        row += rh;

        var lblPriority = new Label { Text = "Priority:", Location = new Point(lx, row + 3), Size = new Size(88, 16), TextAlign = ContentAlignment.MiddleRight };
        _nudPriority = new NumericUpDown { Location = new Point(tx, row), Size = new Size(60, 20), Minimum = 1, Maximum = 10, Value = 5 };
        row += rh;

        _chkActive = new CheckBox { Text = "Active", Location = new Point(tx, row), Size = new Size(80, 18), Checked = true };
        row += rh;

        var lblComments = new Label { Text = "Comments:", Location = new Point(lx, row + 3), Size = new Size(88, 16), TextAlign = ContentAlignment.MiddleRight };
        _txtComments = new TextBox { Location = new Point(tx, row), Size = new Size(252, 36), BorderStyle = BorderStyle.Fixed3D, Multiline = true };
        row += 42;

        _btnOK = new Button { Text = "OK", Location = new Point(tx + 112, row), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnOK.Click += BtnOK_Click;
        _btnCancel = new Button { Text = "Cancel", Location = new Point(tx + 186, row), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            lblName, _txtName, lblType, _cmbType, lblTime, _dtpTime,
            lblDays, _chkMon, _chkTue, _chkWed, _chkThu, _chkFri, _chkSat, _chkSun,
            lblPriority, _nudPriority, _chkActive, lblComments, _txtComments,
            _btnOK, _btnCancel
        });

        ClientSize = new Size(364, row + 32);
    }

    private void PopulateFields()
    {
        if (BroadcastEvent == null) return;
        _txtName.Text = BroadcastEvent.Name;
        _cmbType.SelectedItem = BroadcastEvent.Type;
        _dtpTime.Value = DateTime.Today + BroadcastEvent.ScheduledTime;
        var days = BroadcastEvent.ValidDays;
        _chkMon.Checked = (days & DayOfWeekFlags.Monday) != 0;
        _chkTue.Checked = (days & DayOfWeekFlags.Tuesday) != 0;
        _chkWed.Checked = (days & DayOfWeekFlags.Wednesday) != 0;
        _chkThu.Checked = (days & DayOfWeekFlags.Thursday) != 0;
        _chkFri.Checked = (days & DayOfWeekFlags.Friday) != 0;
        _chkSat.Checked = (days & DayOfWeekFlags.Saturday) != 0;
        _chkSun.Checked = (days & DayOfWeekFlags.Sunday) != 0;
        _nudPriority.Value = Math.Clamp(BroadcastEvent.Priority, 1, 10);
        _chkActive.Checked = BroadcastEvent.IsActive;
        _txtComments.Text = BroadcastEvent.Comments;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Name is required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        BroadcastEvent ??= new BroadcastEvent();
        BroadcastEvent.Name = _txtName.Text.Trim();
        BroadcastEvent.Type = (EventType)(_cmbType.SelectedItem ?? EventType.Music);
        BroadcastEvent.ScheduledTime = _dtpTime.Value.TimeOfDay;

        DayOfWeekFlags days = DayOfWeekFlags.None;
        if (_chkMon.Checked) days |= DayOfWeekFlags.Monday;
        if (_chkTue.Checked) days |= DayOfWeekFlags.Tuesday;
        if (_chkWed.Checked) days |= DayOfWeekFlags.Wednesday;
        if (_chkThu.Checked) days |= DayOfWeekFlags.Thursday;
        if (_chkFri.Checked) days |= DayOfWeekFlags.Friday;
        if (_chkSat.Checked) days |= DayOfWeekFlags.Saturday;
        if (_chkSun.Checked) days |= DayOfWeekFlags.Sunday;
        BroadcastEvent.ValidDays = days;

        BroadcastEvent.Priority = (int)_nudPriority.Value;
        BroadcastEvent.IsActive = _chkActive.Checked;
        BroadcastEvent.Comments = _txtComments.Text.Trim();
    }
}
