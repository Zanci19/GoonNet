using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// LogWorks – log processor that presents played songs and spots in a user-friendly fashion
/// with all relevant data (artist, title, duration, time, log type).
/// </summary>
public class LogWorksForm : Form
{
    public LogDatabase LogDb { get; set; } = null!;

    private ListView _lvLog = null!;
    private DateTimePicker _dtpFrom = null!;
    private DateTimePicker _dtpTo = null!;
    private ComboBox _cboType = null!;
    private TextBox _txtSearch = null!;
    private Button _btnFilter = null!;
    private Button _btnExport = null!;
    private Label _lblCount = null!;
    private Label _lblTotalMusic = null!;
    private Label _lblTotalAds = null!;

    public LogWorksForm()
    {
        InitializeComponent();
        Load += (s, e) => ApplyFilter();
    }

    private void InitializeComponent()
    {
        Text = "LogWorks – Broadcast Log Processor";
        Size = new Size(1000, 640);
        MinimumSize = new Size(800, 480);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        // ── Filter bar ──────────────────────────────────────────────────────
        var filterBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = SystemColors.Control };

        var lblFrom = new Label { Text = "From:", Location = new Point(6, 9), Size = new Size(36, 16) };
        _dtpFrom = new DateTimePicker { Location = new Point(44, 5), Size = new Size(130, 22), Format = DateTimePickerFormat.Short, Value = DateTime.Today };

        var lblTo = new Label { Text = "To:", Location = new Point(180, 9), Size = new Size(24, 16) };
        _dtpTo = new DateTimePicker { Location = new Point(206, 5), Size = new Size(130, 22), Format = DateTimePickerFormat.Short, Value = DateTime.Today };

        var lblType = new Label { Text = "Type:", Location = new Point(344, 9), Size = new Size(36, 16) };
        _cboType = new ComboBox { Location = new Point(382, 5), Size = new Size(120, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        _cboType.Items.Add("All");
        foreach (var v in Enum.GetValues<EventType>()) _cboType.Items.Add(v);
        _cboType.SelectedIndex = 0;

        var lblSearch = new Label { Text = "Search:", Location = new Point(510, 9), Size = new Size(48, 16) };
        _txtSearch = new TextBox { Location = new Point(560, 5), Size = new Size(180, 22) };

        _btnFilter = new Button { Text = "🔍 Apply", Location = new Point(748, 5), Size = new Size(80, 24), FlatStyle = FlatStyle.System };
        _btnFilter.Click += (s, e) => ApplyFilter();

        _btnExport = new Button { Text = "📄 Export CSV", Location = new Point(836, 5), Size = new Size(100, 24), FlatStyle = FlatStyle.System };
        _btnExport.Click += BtnExport_Click;

        filterBar.Controls.AddRange(new Control[] { lblFrom, _dtpFrom, lblTo, _dtpTo, lblType, _cboType, lblSearch, _txtSearch, _btnFilter, _btnExport });

        // ── Summary bar ─────────────────────────────────────────────────────
        var summaryBar = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = SystemColors.Control };
        _lblCount = new Label { Text = "0 entries", Location = new Point(6, 6), Size = new Size(100, 16), Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold) };
        _lblTotalMusic = new Label { Text = "Music: 0", Location = new Point(120, 6), Size = new Size(120, 16) };
        _lblTotalAds = new Label { Text = "Ads: 0", Location = new Point(250, 6), Size = new Size(120, 16) };
        summaryBar.Controls.AddRange(new Control[] { _lblCount, _lblTotalMusic, _lblTotalAds });

        // ── Log list ────────────────────────────────────────────────────────
        _lvLog = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvLog.Columns.Add("Time", 140);
        _lvLog.Columns.Add("Type", 80);
        _lvLog.Columns.Add("Artist", 180);
        _lvLog.Columns.Add("Title", 220);
        _lvLog.Columns.Add("File", 200);
        _lvLog.Columns.Add("Station", 100);
        _lvLog.Columns.Add("Notes", 200);

        Controls.Add(_lvLog);
        Controls.Add(summaryBar);
        Controls.Add(filterBar);
    }

    private void ApplyFilter()
    {
        if (LogDb == null) return;

        DateTime from = _dtpFrom.Value.Date;
        DateTime to = _dtpTo.Value.Date.AddDays(1).AddSeconds(-1);
        string search = _txtSearch.Text.Trim();

        var entries = LogDb.GetByDateRange(from, to).ToList();

        // Type filter
        if (_cboType.SelectedItem is EventType et)
            entries = entries.Where(e => e.EventType == et).ToList();

        // Text search
        if (!string.IsNullOrWhiteSpace(search))
            entries = entries.Where(e =>
                e.Artist.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.FileName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        _lvLog.BeginUpdate();
        _lvLog.Items.Clear();
        int musicCount = 0, adCount = 0;

        foreach (var entry in entries.OrderByDescending(e => e.Timestamp))
        {
            var lvi = new ListViewItem(entry.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));
            lvi.SubItems.Add(entry.EventType.ToString());
            lvi.SubItems.Add(entry.Artist);
            lvi.SubItems.Add(entry.Title);
            lvi.SubItems.Add(entry.FileName);
            lvi.SubItems.Add(entry.ComputerId);
            lvi.SubItems.Add(entry.Notes);

            switch (entry.EventType)
            {
                case EventType.Music: lvi.BackColor = Color.FromArgb(235, 255, 235); musicCount++; break;
                case EventType.Ad: lvi.BackColor = Color.FromArgb(255, 240, 220); adCount++; break;
                case EventType.Jingle: lvi.BackColor = Color.FromArgb(235, 240, 255); break;
            }
            _lvLog.Items.Add(lvi);
        }

        _lvLog.EndUpdate();
        _lblCount.Text = $"{entries.Count} entries";
        _lblTotalMusic.Text = $"Music: {musicCount}";
        _lblTotalAds.Text = $"Ads: {adCount}";
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Export Log to CSV",
            Filter = "CSV Files|*.csv|All Files|*.*",
            FileName = $"GoonNet_Log_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            using var writer = new System.IO.StreamWriter(dlg.FileName, append: false, System.Text.Encoding.UTF8);
            writer.WriteLine("Time,Type,Artist,Title,File,Station,Notes");
            foreach (ListViewItem lvi in _lvLog.Items)
                writer.WriteLine(string.Join(",", lvi.SubItems.Cast<ListViewItem.ListViewSubItem>()
                    .Select(s => $"\"{s.Text.Replace("\"", "\"\"")}\"")));
            MessageBox.Show($"Exported {_lvLog.Items.Count} entries to:\n{dlg.FileName}", "LogWorks");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export error:\n{ex.Message}", "LogWorks");
        }
    }
}
