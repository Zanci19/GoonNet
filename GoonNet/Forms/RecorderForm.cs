using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NAudio.Wave;

namespace GoonNet;

/// <summary>
/// Sound recorder, player and simple editor with recording timers.
/// </summary>
public class RecorderForm : Form
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _waveWriter;
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioReader;

    private string? _recordingPath;
    private string? _loadedPath;
    private bool _isRecording;
    private bool _isPlaying;
    private int _timerSeconds;

    // ── Playback / record controls
    private Button _btnRecord = null!;
    private Button _btnStop = null!;
    private Button _btnPlay = null!;
    private Button _btnPause = null!;
    private Button _btnOpen = null!;
    private Button _btnSave = null!;

    // ── Status / waveform display
    private Label _lblStatus = null!;
    private Label _lblDuration = null!;
    private ProgressBar _pbLevel = null!;
    private ProgressBar _pbPlayback = null!;

    // ── Timer controls
    private CheckBox _chkTimer = null!;
    private NumericUpDown _nudTimerMin = null!;
    private NumericUpDown _nudTimerSec = null!;
    private Label _lblTimerCountdown = null!;

    // ── Device selector
    private ComboBox _cboInputDevice = null!;
    private ComboBox _cboBitrate = null!;

    // ── File list
    private ListBox _lstRecordings = null!;
    private string _recordingFolder = string.Empty;

    // ── Timers
    private System.Windows.Forms.Timer _clockTimer = null!;
    private System.Windows.Forms.Timer _positionTimer = null!;

    private static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoonNet");

    public RecorderForm()
    {
        _recordingFolder = Path.Combine(AppDataPath, "Recordings");
        Directory.CreateDirectory(_recordingFolder);
        InitializeComponent();
        PopulateDevices();
        RefreshRecordingList();

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += ClockTimer_Tick;

        _positionTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _positionTimer.Tick += PositionTimer_Tick;

        FormClosed += RecorderForm_Closed;
    }

    private void InitializeComponent()
    {
        Text = "Recorder";
        Size = new Size(780, 520);
        MinimumSize = new Size(640, 440);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        // ── Top: device + controls ──────────────────────────────────────────
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(40, 45, 60) };

        var lblDev = new Label { Text = "Input device:", ForeColor = Color.Silver, Location = new Point(8, 10), Size = new Size(80, 16) };
        _cboInputDevice = new ComboBox
        {
            Location = new Point(92, 7),
            Size = new Size(260, 20),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(55, 60, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Popup
        };

        var lblBit = new Label { Text = "Sample rate:", ForeColor = Color.Silver, Location = new Point(362, 10), Size = new Size(78, 16) };
        _cboBitrate = new ComboBox
        {
            Location = new Point(442, 7),
            Size = new Size(120, 20),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(55, 60, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Popup
        };
        _cboBitrate.Items.AddRange(new object[] { "44100 Hz / 16-bit", "22050 Hz / 16-bit", "8000 Hz / 16-bit" });
        _cboBitrate.SelectedIndex = 0;

        // Buttons
        _btnRecord = MakeBtn("⏺ RECORD", Color.FromArgb(180, 30, 30), Color.White, new Point(8, 36), new Size(110, 36));
        _btnRecord.Click += BtnRecord_Click;

        _btnStop = MakeBtn("■ STOP", Color.FromArgb(60, 60, 80), Color.Silver, new Point(124, 36), new Size(80, 36));
        _btnStop.Click += BtnStop_Click;

        _btnPlay = MakeBtn("▶ PLAY", Color.FromArgb(30, 100, 40), Color.White, new Point(212, 36), new Size(80, 36));
        _btnPlay.Click += BtnPlay_Click;

        _btnPause = MakeBtn("⏸ PAUSE", Color.FromArgb(80, 80, 30), Color.Silver, new Point(298, 36), new Size(80, 36));
        _btnPause.Click += BtnPause_Click;

        _btnOpen = MakeBtn("📂 OPEN", Color.FromArgb(40, 50, 70), Color.Silver, new Point(390, 36), new Size(80, 36));
        _btnOpen.Click += BtnOpen_Click;

        _btnSave = MakeBtn("💾 SAVE AS", Color.FromArgb(40, 50, 70), Color.Silver, new Point(478, 36), new Size(90, 36));
        _btnSave.Click += BtnSave_Click;

        // Level meter
        var lblLvl = new Label { Text = "Level:", ForeColor = Color.Silver, Location = new Point(8, 80), Size = new Size(44, 16) };
        _pbLevel = new ProgressBar { Location = new Point(56, 80), Size = new Size(300, 16), Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };

        // Status
        _lblStatus = new Label
        {
            Text = "Ready",
            ForeColor = Color.FromArgb(180, 200, 255),
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold),
            Location = new Point(370, 80),
            Size = new Size(200, 20)
        };

        _lblDuration = new Label { Text = "0:00:00", ForeColor = Color.Silver, Location = new Point(580, 80), Size = new Size(100, 20) };

        // Playback progress
        var lblPb = new Label { Text = "Progress:", ForeColor = Color.Silver, Location = new Point(8, 102), Size = new Size(60, 16) };
        _pbPlayback = new ProgressBar { Location = new Point(72, 102), Size = new Size(680, 16), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous };

        topPanel.Controls.AddRange(new Control[] {
            lblDev, _cboInputDevice, lblBit, _cboBitrate,
            _btnRecord, _btnStop, _btnPlay, _btnPause, _btnOpen, _btnSave,
            lblLvl, _pbLevel, _lblStatus, _lblDuration,
            lblPb, _pbPlayback
        });
        topPanel.SizeChanged += (s, e) => _pbPlayback.Width = topPanel.Width - 80;

        // ── Timer section ───────────────────────────────────────────────────
        var timerGroup = new GroupBox
        {
            Text = "Recording Timer",
            Location = new Point(0, 130),
            Height = 54,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(35, 38, 52),
            ForeColor = Color.FromArgb(180, 200, 255),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        timerGroup.SizeChanged += (s, e) => timerGroup.Width = ClientSize.Width;

        _chkTimer = new CheckBox { Text = "Enable timer:", Location = new Point(8, 22), ForeColor = Color.Silver, Size = new Size(100, 18) };
        _nudTimerMin = new NumericUpDown { Location = new Point(112, 20), Size = new Size(52, 20), Minimum = 0, Maximum = 120, Value = 5 };
        var lblMin = new Label { Text = "min", ForeColor = Color.Silver, Location = new Point(168, 22), Size = new Size(24, 16) };
        _nudTimerSec = new NumericUpDown { Location = new Point(196, 20), Size = new Size(52, 20), Minimum = 0, Maximum = 59, Value = 0 };
        var lblSec = new Label { Text = "sec", ForeColor = Color.Silver, Location = new Point(252, 22), Size = new Size(24, 16) };
        _lblTimerCountdown = new Label { Text = "", ForeColor = Color.FromArgb(255, 200, 100), Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold), Location = new Point(290, 20), Size = new Size(160, 20) };

        timerGroup.Controls.AddRange(new Control[] { _chkTimer, _nudTimerMin, lblMin, _nudTimerSec, lblSec, _lblTimerCountdown });

        // ── File list ───────────────────────────────────────────────────────
        var listGroup = new GroupBox
        {
            Text = "Recordings",
            Location = new Point(0, 184),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        listGroup.SizeChanged += (s, e) =>
        {
            listGroup.Width = ClientSize.Width;
            listGroup.Height = ClientSize.Height - 184;
        };

        _lstRecordings = new ListBox
        {
            Location = new Point(4, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lstRecordings.DoubleClick += (s, e) => PlaySelectedRecording();
        listGroup.Controls.Add(_lstRecordings);
        listGroup.SizeChanged += (s, e) => _lstRecordings.Size = new Size(listGroup.ClientSize.Width - 8, listGroup.ClientSize.Height - 22);

        var btnRefresh = new Button { Text = "⟳ Refresh", Location = new Point(4, 18), Size = new Size(80, 22), FlatStyle = FlatStyle.System };
        btnRefresh.Click += (s, e) => RefreshRecordingList();

        var btnDelete = new Button { Text = "🗑 Delete", Location = new Point(90, 18), Size = new Size(80, 22), FlatStyle = FlatStyle.System };
        btnDelete.Click += BtnDeleteRecording_Click;

        var btnOpenFolder = new Button { Text = "📂 Open Folder", Location = new Point(180, 18), Size = new Size(110, 22), FlatStyle = FlatStyle.System };
        btnOpenFolder.Click += (s, e) => System.Diagnostics.Process.Start("explorer.exe", _recordingFolder);

        listGroup.Controls.AddRange(new Control[] { btnRefresh, btnDelete, btnOpenFolder });

        SizeChanged += (s, e) =>
        {
            timerGroup.Width = ClientSize.Width;
            listGroup.Width = ClientSize.Width;
            listGroup.Height = ClientSize.Height - 184;
        };

        Controls.AddRange(new Control[] { topPanel, timerGroup, listGroup });
    }

    private static Button MakeBtn(string text, Color bg, Color fg, Point loc, Size size)
    {
        var b = new Button
        {
            Text = text, Location = loc, Size = size,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg, ForeColor = fg,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 90, 110);
        return b;
    }

    private void PopulateDevices()
    {
        _cboInputDevice.Items.Clear();
        int count = WaveIn.DeviceCount;
        for (int i = 0; i < count; i++)
        {
            var cap = WaveIn.GetCapabilities(i);
            _cboInputDevice.Items.Add(cap.ProductName);
        }
        if (_cboInputDevice.Items.Count > 0)
            _cboInputDevice.SelectedIndex = 0;
        else
            _cboInputDevice.Items.Add("(no input device)");
    }

    private void RefreshRecordingList()
    {
        _lstRecordings.Items.Clear();
        if (!Directory.Exists(_recordingFolder)) return;
        foreach (var f in Directory.GetFiles(_recordingFolder, "*.wav"))
            _lstRecordings.Items.Add(Path.GetFileName(f));
    }

    // ── Recording ──────────────────────────────────────────────────────────

    private void BtnRecord_Click(object? sender, EventArgs e)
    {
        if (_isRecording) return;
        StopPlayback();

        int sampleRate = _cboBitrate.SelectedIndex switch
        {
            1 => 22050,
            2 => 8000,
            _ => 44100
        };

        _recordingPath = Path.Combine(_recordingFolder,
            $"REC_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = Math.Max(0, _cboInputDevice.SelectedIndex),
                WaveFormat = new WaveFormat(sampleRate, 16, 1),
                BufferMilliseconds = 50
            };
            _waveWriter = new WaveFileWriter(_recordingPath, _waveIn.WaveFormat);
            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;
            _waveIn.StartRecording();
            _isRecording = true;
            _timerSeconds = 0;

            _lblStatus.Text = "● RECORDING";
            _lblStatus.ForeColor = Color.Red;
            _btnRecord.BackColor = Color.Red;

            _clockTimer.Start();

            if (_chkTimer.Checked)
            {
                int totalSec = (int)(_nudTimerMin.Value * 60 + _nudTimerSec.Value);
                _timerSeconds = totalSec;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not start recording:\n{ex.Message}", "Recorder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);

        // VU meter
        float max = 0;
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i);
            float level = Math.Abs(sample / 32768f);
            if (level > max) max = level;
        }
        if (IsHandleCreated)
            BeginInvoke(() => _pbLevel.Value = (int)(max * 100));
    }

    private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        _waveWriter?.Dispose();
        _waveWriter = null;
        _waveIn?.Dispose();
        _waveIn = null;
        _isRecording = false;

        if (IsHandleCreated)
            BeginInvoke(() =>
            {
                _lblStatus.Text = "Stopped";
                _lblStatus.ForeColor = Color.FromArgb(180, 200, 255);
                _btnRecord.BackColor = Color.FromArgb(180, 30, 30);
                _pbLevel.Value = 0;
                RefreshRecordingList();
                if (_recordingPath != null)
                    _loadedPath = _recordingPath;
            });
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            _clockTimer.Stop();
            _waveIn?.StopRecording();
        }
        StopPlayback();
    }

    // ── Playback ───────────────────────────────────────────────────────────

    private void BtnPlay_Click(object? sender, EventArgs e)
    {
        if (_isPlaying) { ResumePlayback(); return; }
        string? path = GetSelectedOrLoadedPath();
        if (path == null) { MessageBox.Show("Select a recording first.", "Recorder"); return; }
        StartPlayback(path);
    }

    private string? GetSelectedOrLoadedPath()
    {
        if (_lstRecordings.SelectedItem is string name)
            return Path.Combine(_recordingFolder, name);
        return _loadedPath;
    }

    private void StartPlayback(string path)
    {
        StopPlayback();
        try
        {
            _audioReader = new AudioFileReader(path);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.PlaybackStopped += (s, e) =>
            {
                _isPlaying = false;
                if (IsHandleCreated) BeginInvoke(() =>
                {
                    _lblStatus.Text = "Ready";
                    _pbPlayback.Value = 0;
                    _positionTimer.Stop();
                });
            };
            _waveOut.Play();
            _isPlaying = true;
            _loadedPath = path;
            _lblStatus.Text = "▶ PLAYING";
            _lblStatus.ForeColor = Color.FromArgb(100, 255, 100);
            _positionTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Playback error:\n{ex.Message}", "Recorder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BtnPause_Click(object? sender, EventArgs e)
    {
        if (_isPlaying) PausePlayback();
    }

    private void PausePlayback()
    {
        _waveOut?.Pause();
        _isPlaying = false;
        _lblStatus.Text = "⏸ PAUSED";
        _lblStatus.ForeColor = Color.Orange;
    }

    private void ResumePlayback()
    {
        _waveOut?.Play();
        _isPlaying = true;
        _lblStatus.Text = "▶ PLAYING";
        _lblStatus.ForeColor = Color.FromArgb(100, 255, 100);
    }

    private void StopPlayback()
    {
        _positionTimer.Stop();
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _audioReader?.Dispose();
        _audioReader = null;
        _isPlaying = false;
        if (IsHandleCreated)
        {
            _lblStatus.Text = "Ready";
            _lblStatus.ForeColor = Color.FromArgb(180, 200, 255);
            _pbPlayback.Value = 0;
        }
    }

    private void PlaySelectedRecording()
    {
        if (_lstRecordings.SelectedItem is string name)
            StartPlayback(Path.Combine(_recordingFolder, name));
    }

    // ── File operations ────────────────────────────────────────────────────

    private void BtnOpen_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Open Audio File",
            Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.flac;*.aac|All Files|*.*",
            InitialDirectory = _recordingFolder
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _loadedPath = dlg.FileName;
            _lblStatus.Text = Path.GetFileName(_loadedPath);
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        string? src = GetSelectedOrLoadedPath();
        if (src == null) { MessageBox.Show("No recording to save.", "Recorder"); return; }
        using var dlg = new SaveFileDialog
        {
            Title = "Save Recording As",
            Filter = "WAV Files|*.wav|All Files|*.*",
            FileName = Path.GetFileName(src)
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try { File.Copy(src, dlg.FileName, overwrite: true); }
            catch (Exception ex) { MessageBox.Show($"Save error:\n{ex.Message}", "Recorder"); }
        }
    }

    private void BtnDeleteRecording_Click(object? sender, EventArgs e)
    {
        if (_lstRecordings.SelectedItem is not string name) return;
        string path = Path.Combine(_recordingFolder, name);
        if (MessageBox.Show($"Delete '{name}'?", "Recorder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                StopPlayback();
                File.Delete(path);
                RefreshRecordingList();
            }
            catch (Exception ex) { MessageBox.Show($"Delete error:\n{ex.Message}", "Recorder"); }
        }
    }

    // ── Timers ─────────────────────────────────────────────────────────────

    private int _elapsedSeconds;

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        _elapsedSeconds++;
        _lblDuration.Text = TimeSpan.FromSeconds(_elapsedSeconds).ToString(@"h\:mm\:ss");

        if (_chkTimer.Checked && _isRecording)
        {
            int remaining = _timerSeconds - _elapsedSeconds;
            if (remaining <= 0)
            {
                _clockTimer.Stop();
                _waveIn?.StopRecording();
                _lblTimerCountdown.Text = "Time up!";
            }
            else
            {
                _lblTimerCountdown.Text = $"Stops in: {TimeSpan.FromSeconds(remaining):m\\:ss}";
            }
        }
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_audioReader == null || _waveOut == null) return;
        double fraction = _audioReader.TotalTime.TotalSeconds > 0
            ? _audioReader.CurrentTime.TotalSeconds / _audioReader.TotalTime.TotalSeconds
            : 0;
        _pbPlayback.Value = Math.Clamp((int)(fraction * 1000), 0, 1000);
        _lblDuration.Text = _audioReader.CurrentTime.ToString(@"h\:mm\:ss");
    }

    private void RecorderForm_Closed(object? sender, FormClosedEventArgs e)
    {
        _clockTimer.Stop();
        _positionTimer.Stop();
        if (_isRecording) _waveIn?.StopRecording();
        StopPlayback();
    }
}
