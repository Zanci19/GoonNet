using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public class StreamingForm : Form
{
    private Label _lblStatusIcon = null!;
    private Label _lblStatus = null!;
    private Label _lblListeners = null!;
    private LinkLabel _lnkUrl = null!;
    private Button _btnStartStop = null!;
    private NumericUpDown _nudPort = null!;
    private ComboBox _cboBitRate = null!;
    private TextBox _txtStationName = null!;
    private ListBox _lbLog = null!;
    private System.Windows.Forms.Timer _updateTimer = null!;

    // Stored handler references so they can be unsubscribed when the form closes
    private EventHandler<string>? _statusChangedHandler;
    private EventHandler<StreamClientEventArgs>? _clientConnectedHandler;
    private EventHandler<StreamClientEventArgs>? _clientDisconnectedHandler;

    public StreamingForm()
    {
        InitializeComponent();
        SubscribeToManager();
        _updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _updateTimer.Tick += (s, e) => RefreshStatus();
        _updateTimer.Start();
        RefreshStatus();
    }

    private void InitializeComponent()
    {
        Text = "Web Streaming";
        Size = new Size(520, 520);
        MinimumSize = new Size(420, 420);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        // ---- Status Panel ----
        var statusPanel = new GroupBox
        {
            Text = "STATUS",
            Location = new Point(8, 8),
            Size = new Size(488, 80),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };

        _lblStatusIcon = new Label
        {
            Text = "●",
            ForeColor = Color.Red,
            Font = new Font("Microsoft Sans Serif", 20f, FontStyle.Bold),
            Location = new Point(8, 18),
            Size = new Size(32, 30),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblStatus = new Label
        {
            Text = "Stream is offline",
            Font = new Font("Microsoft Sans Serif", 10f),
            Location = new Point(44, 18),
            Size = new Size(300, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblListeners = new Label
        {
            Text = "Listeners: 0",
            Location = new Point(44, 42),
            Size = new Size(140, 16)
        };

        _lnkUrl = new LinkLabel
        {
            Text = "(not streaming)",
            Location = new Point(44, 58),
            Size = new Size(430, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _lnkUrl.LinkClicked += (s, e) =>
        {
            if (StreamManager.Instance.IsStreaming)
            {
                try { Process.Start(new ProcessStartInfo(_lnkUrl.Text) { UseShellExecute = true }); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open URL:\n{ex.Message}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        };

        statusPanel.Controls.AddRange(new Control[] { _lblStatusIcon, _lblStatus, _lblListeners, _lnkUrl });

        // ---- Configuration Panel ----
        var configPanel = new GroupBox
        {
            Text = "CONFIGURATION",
            Location = new Point(8, 96),
            Size = new Size(488, 120),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };

        var lblPort = new Label { Text = "HTTP Port:", Location = new Point(8, 22), Size = new Size(90, 18), TextAlign = ContentAlignment.MiddleRight };
        _nudPort = new NumericUpDown
        {
            Location = new Point(102, 20),
            Size = new Size(80, 20),
            Minimum = 1024,
            Maximum = 65535,
            Value = StreamManager.Instance.Port,
            BorderStyle = BorderStyle.Fixed3D
        };

        var lblBitRate = new Label { Text = "Bitrate:", Location = new Point(200, 22), Size = new Size(60, 18), TextAlign = ContentAlignment.MiddleRight };
        _cboBitRate = new ComboBox
        {
            Location = new Point(264, 20),
            Size = new Size(90, 21),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboBitRate.Items.AddRange(new object[] { "64 kbps", "96 kbps", "128 kbps", "192 kbps", "256 kbps", "320 kbps" });
        _cboBitRate.SelectedIndex = 2; // 128 kbps default

        var lblName = new Label { Text = "Station Name:", Location = new Point(8, 52), Size = new Size(90, 18), TextAlign = ContentAlignment.MiddleRight };
        _txtStationName = new TextBox
        {
            Location = new Point(102, 50),
            Size = new Size(370, 20),
            Text = StreamManager.Instance.StationName,
            BorderStyle = BorderStyle.Fixed3D,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblInfo = new Label
        {
            Text = "Listeners connect to: http://[your-ip]:[port]/stream  |  Info page: http://[your-ip]:[port]/",
            Location = new Point(8, 82),
            Size = new Size(470, 28),
            ForeColor = Color.DimGray,
            Font = new Font("Microsoft Sans Serif", 7.5f),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        configPanel.Controls.AddRange(new Control[] { lblPort, _nudPort, lblBitRate, _cboBitRate, lblName, _txtStationName, lblInfo });

        // ---- Start/Stop Button ----
        _btnStartStop = new Button
        {
            Text = "▶ START STREAMING",
            Location = new Point(8, 224),
            Size = new Size(488, 36),
            FlatStyle = FlatStyle.System,
            Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold),
            BackColor = Color.FromArgb(200, 255, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _btnStartStop.Click += BtnStartStop_Click;

        // ---- Log Panel ----
        var logPanel = new GroupBox
        {
            Text = "CONNECTION LOG",
            Location = new Point(8, 268),
            Size = new Size(488, 190),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };

        _lbLog = new ListBox
        {
            Location = new Point(6, 18),
            Size = new Size(474, 162),
            Font = new Font("Consolas", 7.5f),
            BorderStyle = BorderStyle.Fixed3D,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            HorizontalScrollbar = true
        };
        logPanel.Controls.Add(_lbLog);

        Controls.AddRange(new Control[] { statusPanel, configPanel, _btnStartStop, logPanel });
    }

    private void SubscribeToManager()
    {
        _statusChangedHandler = (s, msg) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                AddLog(msg);
                RefreshStatus();
            });
        };

        _clientConnectedHandler = (s, e) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                AddLog($"[+] Client connected: {e.ClientEndpoint}  ({e.ListenerCount} total)");
                RefreshStatus();
            });
        };

        _clientDisconnectedHandler = (s, e) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                AddLog($"[-] Client disconnected: {e.ClientEndpoint}  ({e.ListenerCount} remaining)");
                RefreshStatus();
            });
        };

        StreamManager.Instance.StatusChanged += _statusChangedHandler;
        StreamManager.Instance.ClientConnected += _clientConnectedHandler;
        StreamManager.Instance.ClientDisconnected += _clientDisconnectedHandler;
    }

    private void BtnStartStop_Click(object? sender, EventArgs e)
    {
        if (StreamManager.Instance.IsStreaming)
        {
            StreamManager.Instance.Stop();
        }
        else
        {
            // Apply settings
            StreamManager.Instance.Port = (int)_nudPort.Value;
            StreamManager.Instance.StationName = _txtStationName.Text.Trim().Length > 0
                ? _txtStationName.Text.Trim()
                : "GoonNet Radio";
            StreamManager.Instance.BitRate = _cboBitRate.SelectedIndex switch
            {
                0 => 64,
                1 => 96,
                2 => 128,
                3 => 192,
                4 => 256,
                5 => 320,
                _ => 128
            };
            StreamManager.Instance.Start();
        }
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        bool on = StreamManager.Instance.IsStreaming;
        _lblStatusIcon.ForeColor = on ? Color.Lime : Color.Red;
        _lblStatus.Text = on
            ? $"Streaming — {StreamManager.Instance.StationName}"
            : "Stream is offline";
        _lblListeners.Text = $"Listeners: {StreamManager.Instance.ListenerCount}";

        if (on)
        {
            string localIp = StreamManager.GetLocalIpAddress();
            string lanUrl = StreamManager.Instance.IsNetworkWide && localIp != "localhost"
                ? $"http://{localIp}:{StreamManager.Instance.Port}/stream"
                : $"http://localhost:{StreamManager.Instance.Port}/stream";
            _lnkUrl.Text = lanUrl;
        }
        else
        {
            _lnkUrl.Text = "(not streaming)";
        }

        _btnStartStop.Text = on ? "■ STOP STREAMING" : "▶ START STREAMING";
        _btnStartStop.BackColor = on ? Color.FromArgb(255, 200, 200) : Color.FromArgb(200, 255, 200);

        // Lock config controls while streaming
        _nudPort.Enabled = !on;
        _cboBitRate.Enabled = !on;
        _txtStationName.Enabled = !on;
    }

    private void AddLog(string msg)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        _lbLog.Items.Add(entry);
        if (_lbLog.Items.Count > 200)
            _lbLog.Items.RemoveAt(0);
        _lbLog.TopIndex = _lbLog.Items.Count - 1;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _updateTimer.Stop();
        _updateTimer.Dispose();
        StreamManager.Instance.StatusChanged -= _statusChangedHandler;
        StreamManager.Instance.ClientConnected -= _clientConnectedHandler;
        StreamManager.Instance.ClientDisconnected -= _clientDisconnectedHandler;
        base.OnFormClosed(e);
    }
}
