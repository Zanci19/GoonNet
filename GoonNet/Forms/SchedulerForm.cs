using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class SchedulerForm : Form
{
    public EventDatabase EventDb { get; set; } = null!;

    private ListView _lvEvents = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnToggle = null!;

    public SchedulerForm()
    {
        InitializeComponent();
        Load += SchedulerForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Event Scheduler";
        Size = new Size(760, 480);
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

        toolPanel.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete, _btnToggle });

        _lvEvents = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvEvents.Columns.Add("Time", 70);
        _lvEvents.Columns.Add("Name", 180);
        _lvEvents.Columns.Add("Type", 80);
        _lvEvents.Columns.Add("Days", 160);
        _lvEvents.Columns.Add("Active", 60);
        _lvEvents.Columns.Add("Priority", 60);
        _lvEvents.Columns.Add("Comments", 160);
        _lvEvents.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(toolPanel);
        Controls.Add(_lvEvents);
    }

    private void SchedulerForm_Load(object? sender, EventArgs e) => RefreshList();

    private void RefreshList()
    {
        _lvEvents.BeginUpdate();
        _lvEvents.Items.Clear();
        var events = EventDb?.GetAll().OrderBy(ev => ev.ScheduledTime) ?? Enumerable.Empty<BroadcastEvent>();
        foreach (var ev in events)
        {
            var lvi = new ListViewItem(ev.ScheduledTime.ToString(@"hh\:mm\:ss"));
            lvi.SubItems.Add(ev.Name);
            lvi.SubItems.Add(ev.Type.ToString());
            lvi.SubItems.Add(ev.ValidDays.ToString());
            lvi.SubItems.Add(ev.IsActive ? "Yes" : "No");
            lvi.SubItems.Add(ev.Priority.ToString());
            lvi.SubItems.Add(ev.Comments);
            lvi.Tag = ev;
            if (!ev.IsActive) lvi.ForeColor = SystemColors.GrayText;
            _lvEvents.Items.Add(lvi);
        }
        _lvEvents.EndUpdate();
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new EventEditorForm();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.BroadcastEvent != null)
        {
            EventDb?.Add(dlg.BroadcastEvent);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_lvEvents.SelectedItems.Count == 0) return;
        var ev = (BroadcastEvent?)_lvEvents.SelectedItems[0].Tag;
        if (ev == null) return;
        using var dlg = new EventEditorForm(ev);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            EventDb?.Update(ev);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_lvEvents.SelectedItems.Count == 0) return;
        var ev = (BroadcastEvent?)_lvEvents.SelectedItems[0].Tag;
        if (ev == null) return;
        if (MessageBox.Show($"Delete event '{ev.Name}'?", "GoonNet",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            EventDb?.Delete(ev.Id);
            RefreshList();
        }
    }

    private void BtnToggle_Click(object? sender, EventArgs e)
    {
        if (_lvEvents.SelectedItems.Count == 0) return;
        var ev = (BroadcastEvent?)_lvEvents.SelectedItems[0].Tag;
        if (ev == null) return;
        ev.IsActive = !ev.IsActive;
        EventDb?.Update(ev);
        RefreshList();
    }
}
