using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Detailed broadcast log – shows all system events with full technical detail.
/// This is the third log in addition to the normal broadcast log (LogViewerForm) and the error log (ErrorLogForm).
/// </summary>
public class DetailedLogForm : Form
{
    private ListBox _lstLog = null!;
    private Button _btnClear = null!;
    private Button _btnExport = null!;
    private CheckBox _chkAutoScroll = null!;
    private Label _lblCount = null!;

    // Static in-memory detailed log; entries are added by the audio engine and other subsystems.
    private static readonly System.Collections.Concurrent.ConcurrentQueue<DetailedLogEntry> _logQueue = new();
    private static int _totalCount;

    public DetailedLogForm()
    {
        InitializeComponent();
        Load += (s, e) => PopulateExisting();
        // Subscribe to events
        AudioEngine.Instance.TrackStarted += OnTrackStarted;
        AudioEngine.Instance.TrackEnded += OnTrackEnded;
        ErrorLog.Instance.ErrorAdded += OnErrorAdded;
        FormClosed += (s, e) =>
        {
            AudioEngine.Instance.TrackStarted -= OnTrackStarted;
            AudioEngine.Instance.TrackEnded -= OnTrackEnded;
            ErrorLog.Instance.ErrorAdded -= OnErrorAdded;
        };
    }

    private void InitializeComponent()
    {
        Text = "Detailed System Log";
        Size = new Size(920, 580);
        MinimumSize = new Size(700, 400);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = SystemColors.Control };

        var lblTitle = new Label { Text = "DETAILED LOG", Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold), Location = new Point(6, 7), Size = new Size(120, 18) };

        _chkAutoScroll = new CheckBox { Text = "Auto-scroll", Location = new Point(136, 7), Checked = true, Size = new Size(90, 18) };

        _btnClear = new Button
        {
            Text = "Clear",
            Location = new Point(236, 4),
            Size = new Size(60, 22),
            FlatStyle = FlatStyle.System
        };
        _btnClear.Click += (s, e) =>
        {
            _lstLog.Items.Clear();
            while (_logQueue.TryDequeue(out _)) { }
            _totalCount = 0;
            UpdateCount();
        };

        _btnExport = new Button
        {
            Text = "Export...",
            Location = new Point(302, 4),
            Size = new Size(72, 22),
            FlatStyle = FlatStyle.System
        };
        _btnExport.Click += BtnExport_Click;

        _lblCount = new Label { Text = "0 entries", Location = new Point(386, 7), Size = new Size(120, 18) };

        toolBar.Controls.AddRange(new Control[] { lblTitle, _chkAutoScroll, _btnClear, _btnExport, _lblCount });

        _lstLog = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 8.5f),
            BorderStyle = BorderStyle.Fixed3D,
            SelectionMode = SelectionMode.MultiExtended,
            HorizontalScrollbar = true,
            IntegralHeight = false
        };

        Controls.Add(_lstLog);
        Controls.Add(toolBar);
    }

    private void PopulateExisting()
    {
        _lstLog.BeginUpdate();
        foreach (var entry in _logQueue)
            _lstLog.Items.Add(entry.Formatted);
        _lstLog.EndUpdate();
        UpdateCount();
    }

    private void AddLine(string category, string message, Color? color = null)
    {
        var entry = new DetailedLogEntry(DateTime.Now, category, message);
        _logQueue.Enqueue(entry);

        // Keep queue bounded: remove oldest entry when capacity is reached
        if (_logQueue.Count > 5000)
            _logQueue.TryDequeue(out _);

        _totalCount++;

        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            _lstLog.Items.Add(entry.Formatted);
            UpdateCount();
            if (_chkAutoScroll.Checked && _lstLog.Items.Count > 0)
                _lstLog.TopIndex = _lstLog.Items.Count - 1;
        });
    }

    private void UpdateCount() => _lblCount.Text = $"{_lstLog.Items.Count} entries";

    // ── Static helpers so other classes can write to the detailed log ───────

    public static void Log(string category, string message) =>
        _logQueue.Enqueue(new DetailedLogEntry(DateTime.Now, category, message));

    // ── AudioEngine subscriptions ───────────────────────────────────────────

    private void OnTrackStarted(object? sender, TrackEventArgs e)
    {
        if (!IsHandleCreated) return;
        string dev = e.Device == AudioDeviceType.Main ? "MAIN" : "PREVIEW";
        AddLine("PLAY", $"[{dev}] {e.Track?.Artist} – {e.Track?.Title}  ({e.Track?.FileName})");
    }

    private void OnTrackEnded(object? sender, TrackEventArgs e)
    {
        if (!IsHandleCreated) return;
        string dev = e.Device == AudioDeviceType.Main ? "MAIN" : "PREVIEW";
        AddLine("END ", $"[{dev}] track ended");
    }

    private void OnErrorAdded(object? sender, ErrorLogEntry entry)
    {
        if (!IsHandleCreated) return;
        AddLine("ERR ", $"[{entry.Source}] {entry.Message}");
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Export Detailed Log",
            Filter = "Text Files|*.txt|All Files|*.*",
            FileName = $"GoonNet_DetailedLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            System.IO.File.WriteAllLines(dlg.FileName,
                System.Linq.Enumerable.Range(0, _lstLog.Items.Count)
                    .Select(i => _lstLog.Items[i]?.ToString() ?? string.Empty));
            MessageBox.Show($"Exported {_lstLog.Items.Count} lines.", "Detailed Log");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export error:\n{ex.Message}", "Detailed Log");
        }
    }
}

public record DetailedLogEntry(DateTime Timestamp, string Category, string Message)
{
    public string Formatted => $"{Timestamp:HH:mm:ss.fff}  [{Category,-4}]  {Message}";
}
