using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class LogViewerForm : Form
{
    public LogDatabase LogDb { get; set; } = null!;

    private ListView _lvLog = null!;
    private DateTimePicker _dtpFrom = null!;
    private DateTimePicker _dtpTo = null!;
    private Button _btnFilter = null!;
    private Button _btnRefresh = null!;
    private Label _lblCount = null!;

    public LogViewerForm()
    {
        InitializeComponent();
        Load += LogViewerForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Broadcast Log Viewer";
        Size = new Size(760, 480);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 32 };

        var lblFrom = new Label { Text = "From:", Location = new Point(4, 8), Size = new Size(38, 16) };
        _dtpFrom = new DateTimePicker { Location = new Point(44, 4), Size = new Size(130, 22), Format = DateTimePickerFormat.Short };
        _dtpFrom.Value = DateTime.Today;

        var lblTo = new Label { Text = "To:", Location = new Point(178, 8), Size = new Size(26, 16) };
        _dtpTo = new DateTimePicker { Location = new Point(206, 4), Size = new Size(130, 22), Format = DateTimePickerFormat.Short };
        _dtpTo.Value = DateTime.Today;

        _btnFilter = new Button { Text = "Filter", Location = new Point(340, 4), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnFilter.Click += (s, e) => RefreshLog();

        _btnRefresh = new Button { Text = "Refresh", Location = new Point(404, 4), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnRefresh.Click += (s, e) => RefreshAll();

        _lblCount = new Label { Text = "0 entries", Location = new Point(474, 8), Size = new Size(120, 16) };

        toolPanel.Controls.AddRange(new Control[] { lblFrom, _dtpFrom, lblTo, _dtpTo, _btnFilter, _btnRefresh, _lblCount });

        _lvLog = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvLog.Columns.Add("Time", 120);
        _lvLog.Columns.Add("Artist", 140);
        _lvLog.Columns.Add("Title", 160);
        _lvLog.Columns.Add("Type", 70);
        _lvLog.Columns.Add("File", 180);
        _lvLog.Columns.Add("Notes", 140);

        Controls.Add(toolPanel);
        Controls.Add(_lvLog);
    }

    private void LogViewerForm_Load(object? sender, EventArgs e) => RefreshAll();

    private void RefreshAll()
    {
        _dtpFrom.Value = DateTime.Today;
        _dtpTo.Value = DateTime.Today;
        RefreshLog();
    }

    private void RefreshLog()
    {
        _lvLog.BeginUpdate();
        _lvLog.Items.Clear();
        var from = _dtpFrom.Value.Date;
        var to = _dtpTo.Value.Date.AddDays(1).AddSeconds(-1);
        var entries = LogDb?.GetByDateRange(from, to) ?? Enumerable.Empty<LogEntry>();
        int count = 0;
        foreach (var entry in entries)
        {
            var lvi = new ListViewItem(entry.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));
            lvi.SubItems.Add(entry.Artist);
            lvi.SubItems.Add(entry.Title);
            lvi.SubItems.Add(entry.EventType.ToString());
            lvi.SubItems.Add(entry.FileName);
            lvi.SubItems.Add(entry.Notes);
            lvi.Tag = entry;
            _lvLog.Items.Add(lvi);
            count++;
        }
        _lvLog.EndUpdate();
        _lblCount.Text = $"{count} entries";
    }
}
