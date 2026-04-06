using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class StudioForm : Form
{
    public PlaylistDatabase PlaylistDb { get; set; } = null!;
    public MySqlMusicDatabase MusicDb { get; set; } = null!;
    public LogDatabase LogDb { get; set; } = null!;

    private Playlist? _currentPlaylist;
    private int _currentIndex = -1;
    private bool _autoPlay = true;

    // Now Playing panel
    private Label _lblArtist = null!;
    private Label _lblTitle = null!;
    private Label _lblElapsed = null!;
    private Label _lblRemaining = null!;
    private Label _lblIntroValue = null!;
    private Label _lblVoiceOutValue = null!;
    private Label _lblMixInValue = null!;
    private Label _lblOnAir = null!;
    private Label _lblClock = null!;
    private ProgressBar _progressBar = null!;

    // Controls
    private Button _btnPlayPause = null!;
    private Button _btnStop = null!;
    private Button _btnNext = null!;
    private Button _btnCue = null!;
    private Button _btnFadeOut = null!;
    private Button _btnPanic = null!;
    private TrackBar _volumeSlider = null!;
    private Label _lblVolume = null!;
    private CheckBox _chkAutoPlay = null!;
    private CheckBox _chkRadioAway = null!;

    // Reminder
    private System.Windows.Forms.Timer _reminderTimer = null!;
    private Label _lblReminder = null!;
    private System.Collections.Generic.Queue<ReminderEntry> _reminders = new();

    // Up Next panel
    private Label _lblNextArtist = null!;
    private Label _lblNextTitle = null!;
    private Label _lblNextDuration = null!;
    private Button _btnLoadNext = null!;

    // Playlist + library list views
    private ListView _lvPlaylist = null!;
    private ListView _lvLibrary = null!;
    private Button _btnAddToLibrary = null!;
    private Button _btnEditQueuePoints = null!;
    private Button _btnMix2Ch = null!;

    // Preview section
    private Label _lblPreviewTrack = null!;
    private Button _btnPreviewPlay = null!;
    private Button _btnPreviewStop = null!;
    private TrackBar _previewVolumeSlider = null!;

    // Stereo metering
    private VUMeter _vuLeft = null!;
    private VUMeter _vuRight = null!;

    // Streaming status bar
    private Label _lblStreamStatus = null!;
    private Button _btnStreamSettings = null!;

    // Pitch / Speed bar
    private Panel _pitchSpeedBar = null!;
    private TrackBar _pitchSlider = null!;
    private TrackBar _tempoSlider = null!;
    private TrackBar _rateSlider = null!;
    private Label _lblPitchVal = null!;
    private Label _lblTempoVal = null!;
    private Label _lblRateVal = null!;
    private ComboBox _cboPlaylistSelect = null!;
    private Button _btnSaveSession = null!;

    private bool _manualJump;
    private readonly Random _random = new();
    private const int AwayStingerChancePercent = 20;

    private System.Windows.Forms.Timer _clockTimer = null!;

    // SampleAggregator subscription tracking
    private SampleAggregator? _currentAggregator;

    public StudioForm()
    {
        InitializeComponent();
        ConnectAudioEngine();
        _clockTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _clockTimer.Tick += (s, e) =>
        {
            _lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
            CheckReminders();
        };
        _clockTimer.Start();

        _reminderTimer = new System.Windows.Forms.Timer { Interval = 60000 };
        _reminderTimer.Tick += (s, e) => CheckReminders();
        _reminderTimer.Start();

        Load += StudioForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "GoonNet Studio";
        ClientSize = new Size(1024, 768);
        MinimumSize = new Size(980, 700);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(2)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));

        // top info strip
        var topStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            BackColor = Color.Black,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Margin = Padding.Empty
        };
        topStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        topStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        topStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        topStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        topStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        var meterPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        var lblLvl = new Label { Text = "-156.01", ForeColor = Color.Lime, BackColor = Color.Black, Location = new Point(8, 6), Size = new Size(98, 14), Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold) };
        _vuLeft = new VUMeter { Location = new Point(8, 24), Size = new Size(44, 46) };
        _vuRight = new VUMeter { Location = new Point(56, 24), Size = new Size(44, 46) };
        meterPanel.Controls.AddRange(new Control[] { lblLvl, _vuLeft, _vuRight });

        var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        _lblArtist = new Label { Text = "Shadow Gallery - Mystery", ForeColor = Color.Lime, BackColor = Color.Black, Location = new Point(8, 6), Size = new Size(288, 18), Font = new Font("Microsoft Sans Serif", 11f, FontStyle.Bold) };
        _lblTitle = new Label { Text = "DREAM THEATER - LEARNING TO LIVE", ForeColor = Color.Gold, BackColor = Color.Black, Location = new Point(8, 30), Size = new Size(288, 18), Font = new Font("Microsoft Sans Serif", 11f, FontStyle.Italic) };
        _progressBar = new ProgressBar { Location = new Point(8, 54), Size = new Size(288, 14), Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 1000, Value = 0 };
        titlePanel.Controls.AddRange(new Control[] { _lblArtist, _lblTitle, _progressBar });

        var timePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        _lblElapsed = new Label { Text = "34:7.5", ForeColor = Color.Lime, BackColor = Color.Black, Location = new Point(8, 6), Size = new Size(60, 14), Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold) };
        _lblRemaining = new Label { Text = "-17:05.44", ForeColor = Color.Lime, BackColor = Color.Black, Location = new Point(8, 24), Size = new Size(118, 14), Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold) };
        _lblIntroValue = new Label { Text = "0:00", ForeColor = Color.Cyan, BackColor = Color.Black, Location = new Point(8, 44), Size = new Size(36, 14), Font = new Font("Microsoft Sans Serif", 7f, FontStyle.Bold) };
        _lblVoiceOutValue = new Label { Text = "0:00", ForeColor = Color.Orange, BackColor = Color.Black, Location = new Point(48, 44), Size = new Size(36, 14), Font = new Font("Microsoft Sans Serif", 7f, FontStyle.Bold) };
        _lblMixInValue = new Label { Text = "0:00", ForeColor = Color.DeepSkyBlue, BackColor = Color.Black, Location = new Point(88, 44), Size = new Size(36, 14), Font = new Font("Microsoft Sans Serif", 7f, FontStyle.Bold) };
        timePanel.Controls.AddRange(new Control[] { _lblElapsed, _lblRemaining, _lblIntroValue, _lblVoiceOutValue, _lblMixInValue });

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        _lblClock = new Label { Text = DateTime.Now.ToString("HH:mm:ss"), ForeColor = Color.Lime, BackColor = Color.Black, Location = new Point(6, 6), Size = new Size(120, 18), TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _lblOnAir = new Label { Text = "Manko Vitez", ForeColor = Color.Blue, BackColor = Color.Black, Location = new Point(8, 30), Size = new Size(240, 18), Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold) };
        _lblReminder = new Label { Text = string.Empty, ForeColor = Color.Gold, BackColor = Color.Black, Location = new Point(8, 52), Size = new Size(280, 16), Visible = false };
        statusPanel.Controls.AddRange(new Control[] { _lblClock, _lblOnAir, _lblReminder });

        var genrePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        _lblNextArtist = new Label { Text = "ProgMetal", ForeColor = Color.Lime, BackColor = Color.Black, Location = new Point(8, 8), Size = new Size(98, 18), Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold) };
        _lblNextTitle = new Label { Text = "ProgMetal", ForeColor = Color.Gold, BackColor = Color.Black, Location = new Point(8, 34), Size = new Size(98, 18), Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold) };
        _lblNextDuration = new Label { Text = "", Visible = false };
        genrePanel.Controls.AddRange(new Control[] { _lblNextArtist, _lblNextTitle });

        topStrip.Controls.Add(meterPanel, 0, 0);
        topStrip.Controls.Add(titlePanel, 1, 0);
        topStrip.Controls.Add(timePanel, 2, 0);
        topStrip.Controls.Add(statusPanel, 3, 0);
        topStrip.Controls.Add(genrePanel, 4, 0);

        var headerStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Color.Black,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Margin = Padding.Empty
        };
        headerStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 570));
        headerStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        headerStrip.Controls.Add(new Label { Text = "Playlist", ForeColor = Color.Lime, BackColor = Color.Black, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Microsoft Sans Serif", 11f, FontStyle.Italic) }, 0, 0);
        headerStrip.Controls.Add(new Label { Text = string.Empty, BackColor = Color.Black, Dock = DockStyle.Fill }, 1, 0);
        headerStrip.Controls.Add(new Label { Text = "ProgMetal", ForeColor = Color.Lime, BackColor = Color.Black, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Microsoft Sans Serif", 11f, FontStyle.Italic) }, 2, 0);

        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = SystemColors.Control,
            Margin = Padding.Empty
        };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));

        _lvPlaylist = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BackColor = Color.White,
            ForeColor = Color.Black,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvPlaylist.Columns.Add("time", 54);
        _lvPlaylist.Columns.Add("#", 36);
        _lvPlaylist.Columns.Add("Artist / Title", 390);
        _lvPlaylist.Columns.Add("Cat", 90);
        _lvPlaylist.Columns.Add("cue", 64);
        _lvPlaylist.Columns.Add("dur", 64);
        _lvPlaylist.Columns.Add("start", 64);
        _lvPlaylist.Columns.Add("status", 90);
        _lvPlaylist.DoubleClick += LvPlaylist_DoubleClick;
        _lvPlaylist.AllowDrop = true;
        _lvPlaylist.DragEnter += LvPlaylist_DragEnter;
        _lvPlaylist.DragDrop += LvPlaylist_DragDrop;

        var sideButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            BackColor = SystemColors.Control,
            Padding = new Padding(4, 4, 4, 4),
            WrapContents = false
        };

        _btnPlayPause = new Button { Text = "Start", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnPlayPause.Click += BtnPlayPause_Click;
        _btnStop = new Button { Text = "Stop", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnStop.Click += (s, e) => { AudioEngine.Instance.Stop(); UpdatePlaylistStatus(); };
        _btnNext = new Button { Text = "Next", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnNext.Click += BtnNext_Click;
        _btnCue = new Button { Text = "Cue", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnCue.Click += BtnCue_Click;
        _btnFadeOut = new Button { Text = "Fade", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnFadeOut.Click += (s, e) => AudioEngine.Instance.FadeOut(AudioDeviceType.Main, TimeSpan.FromSeconds(5));
        _btnPanic = new Button { Text = "Panic", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnPanic.Click += BtnPanic_Click;
        _btnLoadNext = new Button { Text = "Load", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnLoadNext.Click += BtnLoadNext_Click;
        _btnEditQueuePoints = new Button { Text = "Queue", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnEditQueuePoints.Click += BtnEditQueuePoints_Click;
        _btnMix2Ch = new Button { Text = "2ch", Width = 86, Height = 24, FlatStyle = FlatStyle.System };
        _btnMix2Ch.Click += BtnMix2Ch_Click;

        sideButtons.Controls.AddRange(new Control[] { _btnPlayPause, _btnStop, _btnNext, _btnCue, _btnFadeOut, _btnPanic, _btnLoadNext, _btnEditQueuePoints, _btnMix2Ch });

        center.Controls.Add(_lvPlaylist, 0, 0);
        center.Controls.Add(sideButtons, 1, 0);

        var bottomArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = SystemColors.Control,
            Margin = Padding.Empty
        };
        bottomArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
        bottomArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        bottomArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));

        // ── Transport GroupBox ────────────────────────────────────────────────
        var transportPanel = new GroupBox { Text = "Transport", Dock = DockStyle.Fill };
        var transportTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Margin = new Padding(2),
            Padding = new Padding(4, 2, 4, 2)
        };
        transportTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));  // label
        transportTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // slider
        transportTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));  // value
        transportTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));  // preview buttons
        transportTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));        // checkboxes row
        transportTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));        // main volume
        transportTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));        // cue volume

        // Row 0: checkboxes (span all 4 columns)
        _chkAutoPlay = new CheckBox { Text = "Auto Play", Dock = DockStyle.Fill, Checked = true };
        _chkAutoPlay.CheckedChanged += (s, e) => _autoPlay = _chkAutoPlay.Checked;
        _chkRadioAway = new CheckBox { Text = "Radio Away", Dock = DockStyle.Fill, Checked = false };
        transportTable.Controls.Add(_chkAutoPlay, 0, 0);
        transportTable.SetColumnSpan(_chkAutoPlay, 2);
        transportTable.Controls.Add(_chkRadioAway, 2, 0);
        transportTable.SetColumnSpan(_chkRadioAway, 2);

        // Row 1: Main volume
        var lblVol = new Label { Text = "Main", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _volumeSlider = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Value = 85, TickStyle = TickStyle.None };
        _volumeSlider.ValueChanged += (s, e) => { AudioEngine.Instance.MainVolume = _volumeSlider.Value / 100f; _lblVolume.Text = _volumeSlider.Value + "%"; };
        _lblVolume = new Label { Text = "85%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _btnPreviewPlay = new Button { Text = ">", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
        _btnPreviewPlay.Click += BtnPreviewPlay_Click;
        transportTable.Controls.Add(lblVol, 0, 1);
        transportTable.Controls.Add(_volumeSlider, 1, 1);
        transportTable.Controls.Add(_lblVolume, 2, 1);
        transportTable.Controls.Add(_btnPreviewPlay, 3, 1);

        // Row 2: Cue volume
        var lblPreviewVol = new Label { Text = "Cue", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _previewVolumeSlider = new TrackBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Value = 80, TickStyle = TickStyle.None };
        _previewVolumeSlider.ValueChanged += (s, e) => AudioEngine.Instance.PreviewVolume = _previewVolumeSlider.Value / 100f;
        _lblPreviewTrack = new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _btnPreviewStop = new Button { Text = "[]", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
        _btnPreviewStop.Click += (s, e) => AudioEngine.Instance.Stop(AudioDeviceType.Preview);
        transportTable.Controls.Add(lblPreviewVol, 0, 2);
        transportTable.Controls.Add(_previewVolumeSlider, 1, 2);
        transportTable.Controls.Add(_lblPreviewTrack, 2, 2);
        transportTable.Controls.Add(_btnPreviewStop, 3, 2);
        transportTable.SetColumnSpan(_lblPreviewTrack, 1);

        transportPanel.Controls.Add(transportTable);

        // ── Deck GroupBox (pitch/tempo/rate) — use TableLayoutPanel to prevent overlap ──
        var pitchPanel = new GroupBox { Text = "Deck", Dock = DockStyle.Fill };
        var deckTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Margin = new Padding(2),
            Padding = new Padding(4, 2, 4, 2)
        };
        deckTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40)); // row name label
        deckTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // slider
        deckTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44)); // value label
        deckTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        deckTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        deckTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.4f));

        var lblPitchName = new Label { Text = "Pitch", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _pitchSlider = new TrackBar { Dock = DockStyle.Fill, Minimum = -24, Maximum = 24, TickStyle = TickStyle.None };
        _pitchSlider.ValueChanged += PitchSlider_Changed;
        _lblPitchVal = new Label { Text = "0 st", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        var lblTempoName = new Label { Text = "Tempo", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _tempoSlider = new TrackBar { Dock = DockStyle.Fill, Minimum = -50, Maximum = 100, TickStyle = TickStyle.None };
        _tempoSlider.ValueChanged += TempoSlider_Changed;
        _lblTempoVal = new Label { Text = "0%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        var lblRateName = new Label { Text = "Rate", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _rateSlider = new TrackBar { Dock = DockStyle.Fill, Minimum = -50, Maximum = 100, TickStyle = TickStyle.None };
        _rateSlider.ValueChanged += RateSlider_Changed;
        _lblRateVal = new Label { Text = "x1.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        deckTable.Controls.Add(lblPitchName, 0, 0);
        deckTable.Controls.Add(_pitchSlider, 1, 0);
        deckTable.Controls.Add(_lblPitchVal, 2, 0);
        deckTable.Controls.Add(lblTempoName, 0, 1);
        deckTable.Controls.Add(_tempoSlider, 1, 1);
        deckTable.Controls.Add(_lblTempoVal, 2, 1);
        deckTable.Controls.Add(lblRateName, 0, 2);
        deckTable.Controls.Add(_rateSlider, 1, 2);
        deckTable.Controls.Add(_lblRateVal, 2, 2);
        pitchPanel.Controls.Add(deckTable);

        // ── Library GroupBox with Add-Track toolbar ───────────────────────────
        var libraryPanel = new GroupBox { Text = "Library", Dock = DockStyle.Fill };
        var libToolbar = new Panel { Dock = DockStyle.Top, Height = 26 };
        _btnAddToLibrary = new Button { Text = "+ Add", Location = new Point(4, 3), Size = new Size(56, 20), FlatStyle = FlatStyle.System };
        _btnAddToLibrary.Click += BtnAddToLibrary_Click;
        libToolbar.Controls.Add(_btnAddToLibrary);
        _lvLibrary = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.Fixed3D, BackColor = Color.White, ForeColor = Color.Black };
        _lvLibrary.Columns.Add("Artist", 132);
        _lvLibrary.Columns.Add("Title", 200);
        _lvLibrary.Columns.Add("Dur", 48);
        _lvLibrary.ItemDrag += LvLibrary_ItemDrag;
        libraryPanel.Controls.Add(_lvLibrary);
        libraryPanel.Controls.Add(libToolbar);

        bottomArea.Controls.Add(transportPanel, 0, 0);
        bottomArea.Controls.Add(pitchPanel, 1, 0);
        bottomArea.Controls.Add(libraryPanel, 2, 0);

        _pitchSpeedBar = BuildPitchSpeedBar();
        _pitchSpeedBar.Visible = false;

        var streamBar = new Panel { Height = 1, Dock = DockStyle.Bottom, Visible = false };
        _lblStreamStatus = new Label();
        _btnStreamSettings = new Button();

        root.Controls.Add(topStrip, 0, 0);
        root.Controls.Add(headerStrip, 0, 1);
        root.Controls.Add(center, 0, 2);
        root.Controls.Add(bottomArea, 0, 3);

        Controls.Add(root);
        Controls.Add(_pitchSpeedBar);
        Controls.Add(streamBar);
    }

    private Panel BuildPitchSpeedBar()
    {
        var bar = new Panel
        {
            Height = 32,
            BackColor = Color.FromArgb(20, 22, 34)
        };

        // ── LEFT: Playlist selector + session save ──────────────────────────
        var lblPl = new Label { Text = "Playlist:", Location = new Point(8, 8), Size = new Size(52, 16), ForeColor = Color.Silver, Font = new Font("Microsoft Sans Serif", 7.5f) };

        _cboPlaylistSelect = new ComboBox
        {
            Location = new Point(62, 5),
            Size = new Size(240, 20),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(40, 45, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Popup,
            Font = new Font("Microsoft Sans Serif", 7.5f)
        };
        _cboPlaylistSelect.SelectedIndexChanged += CboPlaylistSelect_Changed;

        var btnLoad = new Button
        {
            Text = "📋 Load",
            Location = new Point(306, 4),
            Size = new Size(62, 22),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 220, 255),
            BackColor = Color.FromArgb(40, 55, 80),
            Font = new Font("Microsoft Sans Serif", 7.5f)
        };
        btnLoad.FlatAppearance.BorderColor = Color.FromArgb(80, 110, 160);
        btnLoad.Click += (s, e) => LoadSelectedPlaylist();

        _btnSaveSession = new Button
        {
            Text = "💾 Save",
            Location = new Point(372, 4),
            Size = new Size(62, 22),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(200, 255, 180),
            BackColor = Color.FromArgb(30, 55, 30),
            Font = new Font("Microsoft Sans Serif", 7.5f)
        };
        _btnSaveSession.FlatAppearance.BorderColor = Color.FromArgb(60, 130, 60);
        _btnSaveSession.Click += (s, e) => { SaveSession(); MessageBox.Show("Session saved.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Information); };

        // ── CENTER/RIGHT: Pitch, Tempo, Rate sliders ────────────────────────
        bar.Controls.AddRange(new Control[] {
            lblPl, _cboPlaylistSelect, btnLoad, _btnSaveSession
        });
        return bar;
    }

    private void PitchSlider_Changed(object? sender, EventArgs e)
    {
        double semitones = _pitchSlider.Value;
        AudioEngine.Instance.PitchSemiTones = semitones;
        _lblPitchVal.Text = semitones == 0 ? "0 st" : (semitones > 0 ? $"+{semitones} st" : $"{semitones} st");
    }

    private void TempoSlider_Changed(object? sender, EventArgs e)
    {
        double pct = _tempoSlider.Value;
        AudioEngine.Instance.TempoChange = pct;
        _lblTempoVal.Text = pct == 0 ? "0%" : (pct > 0 ? $"+{pct}%" : $"{pct}%");
    }

    private void RateSlider_Changed(object? sender, EventArgs e)
    {
        double pct = _rateSlider.Value;
        AudioEngine.Instance.RateChange = pct;
        double mult = 1.0 + pct / 100.0;
        _lblRateVal.Text = $"x{mult:F2}";
    }

    private void ResetPitchSpeed()
    {
        _pitchSlider.Value = 0;
        _tempoSlider.Value = 0;
        _rateSlider.Value = 0;
        AudioEngine.Instance.PitchSemiTones = 0;
        AudioEngine.Instance.TempoChange = 0;
        AudioEngine.Instance.RateChange = 0;
        _lblPitchVal.Text = "0 st";
        _lblTempoVal.Text = "0%";
        _lblRateVal.Text = "x1.00";
    }

    private void CboPlaylistSelect_Changed(object? sender, EventArgs e)
    {
        // Preview only – actual load happens via "Load" button
    }

    private void LoadSelectedPlaylist()
    {
        if (_cboPlaylistSelect.SelectedItem is Playlist pl)
        {
            _currentPlaylist = pl;
            _currentIndex = -1;
            PopulatePlaylistDisplayFields();
            RefreshPlaylistView();
            ShowNextTrack();
        }
    }

    private void PopulatePlaylistCombo()
    {
        _cboPlaylistSelect.Items.Clear();
        foreach (var pl in PlaylistDb?.GetAll() ?? System.Linq.Enumerable.Empty<Playlist>())
            _cboPlaylistSelect.Items.Add(pl);
        _cboPlaylistSelect.DisplayMember = "Name";
    }

    private void PopulatePlaylistDisplayFields()
    {
        if (_currentPlaylist == null) return;
        foreach (var item in _currentPlaylist.Items)
        {
            var track = MusicDb?.GetById(item.TrackId);
            if (track != null)
            {
                item.Artist = track.Artist;
                item.Title = track.Title;
                if (item.Duration == null && track.Duration > TimeSpan.Zero)
                    item.Duration = track.Duration;
            }
        }
    }

    private static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoonNet");

    private string SessionFilePath => Path.Combine(AppDataPath, "studio_session.xml");

    private void SaveSession()
    {
        try
        {
            var session = new StudioSession
            {
                PlaylistId = _currentPlaylist?.Id,
                CurrentIndex = _currentIndex,
                MainVolume = AudioEngine.Instance.MainVolume,
                PreviewVolume = AudioEngine.Instance.PreviewVolume,
                PitchSemiTones = AudioEngine.Instance.PitchSemiTones,
                TempoChange = AudioEngine.Instance.TempoChange,
                RateChange = AudioEngine.Instance.RateChange,
                AutoPlay = _autoPlay
            };
            Directory.CreateDirectory(AppDataPath);
            var ser = new System.Xml.Serialization.XmlSerializer(typeof(StudioSession));
            using var writer = new StreamWriter(SessionFilePath);
            ser.Serialize(writer, session);
        }
        catch { /* non-critical */ }
    }

    private void LoadSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath)) return;
            var ser = new System.Xml.Serialization.XmlSerializer(typeof(StudioSession));
            using var reader = new StreamReader(SessionFilePath);
            if (ser.Deserialize(reader) is not StudioSession session) return;

            // Restore sliders and engine settings
            int volVal = (int)(session.MainVolume * 100);
            _volumeSlider.Value = Math.Clamp(volVal, 0, 100);
            _lblVolume.Text = volVal + "%";
            AudioEngine.Instance.MainVolume = session.MainVolume;

            _previewVolumeSlider.Value = Math.Clamp((int)(session.PreviewVolume * 100), 0, 100);
            AudioEngine.Instance.PreviewVolume = session.PreviewVolume;

            _pitchSlider.Value = Math.Clamp((int)session.PitchSemiTones, -24, 24);
            PitchSlider_Changed(null, EventArgs.Empty);
            _tempoSlider.Value = Math.Clamp((int)session.TempoChange, -50, 100);
            TempoSlider_Changed(null, EventArgs.Empty);
            _rateSlider.Value = Math.Clamp((int)session.RateChange, -50, 100);
            RateSlider_Changed(null, EventArgs.Empty);

            _autoPlay = session.AutoPlay;
            _chkAutoPlay.Checked = _autoPlay;

            // Restore playlist selection
            if (session.PlaylistId.HasValue && PlaylistDb != null)
            {
                var pl = PlaylistDb.GetById(session.PlaylistId.Value);
                if (pl != null)
                {
                    _currentPlaylist = pl;
                    _currentIndex = session.CurrentIndex;
                    if (_currentIndex >= (_currentPlaylist?.Items.Count ?? 0))
                        _currentIndex = -1;

                    // Select in combo
                    foreach (var item in _cboPlaylistSelect.Items)
                    {
                        if (item is Playlist p && p.Id == pl.Id)
                        {
                            _cboPlaylistSelect.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }
        catch { /* non-critical */ }
    }

    private void OpenStreamingForm()
    {
        if (MdiParent != null)
        {
            foreach (Form child in MdiParent.MdiChildren)
                if (child is StreamingForm) { child.Activate(); return; }
            var f = new StreamingForm { MdiParent = MdiParent };
            f.Show();
        }
        else
        {
            using var f = new StreamingForm();
            f.ShowDialog(this);
        }
    }

    private void ConnectAudioEngine()
    {
        // Subscribe to SampleAggregator changes for real spectrum + VU data
        AudioEngine.Instance.MainSampleAggregatorChanged += OnMainAggregatorChanged;

        AudioEngine.Instance.TrackStarted += (s, e) =>
        {
            if (e.Device != AudioDeviceType.Main) return;
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                var t = e.Track;
                if (t == null) return;
                _lblArtist.Text = t.Artist;
                _lblTitle.Text = t.Title;
                _btnPlayPause.Text = "⏸ PAUSE";
                _btnPlayPause.BackColor = Color.FromArgb(255, 255, 180);
                UpdateSegueTimers(TimeSpan.Zero, t);
            });
        };

        AudioEngine.Instance.TrackEnded += (s, e) =>
        {
            if (e.Device != AudioDeviceType.Main) return;
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                _btnPlayPause.Text = "▶ PLAY";
                _btnPlayPause.BackColor = Color.FromArgb(200, 255, 200);
                _progressBar.Value = 0;
                _lblElapsed.Text = "0:00";
                _lblRemaining.Text = "-0:00";
                UpdateSegueTimers(TimeSpan.Zero, null);
                MarkCurrentPlayed();
                if (_autoPlay && !_manualJump) PlayNext();
                _manualJump = false;
            });
        };

        AudioEngine.Instance.TrackPositionChanged += (s, e) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                _progressBar.Value = Math.Clamp((int)(e.Fraction * 1000), 0, 1000);
                _lblElapsed.Text = FormatTime(e.Position);
                _lblRemaining.Text = "-" + FormatTime(e.Duration - e.Position);
                UpdateSegueTimers(e.Position, AudioEngine.Instance.CurrentTrack);
            });
        };

        // Stream status updates
        StreamManager.Instance.StatusChanged += (s, msg) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() => UpdateStreamStatusBar());
        };
        StreamManager.Instance.ClientConnected += (s, e) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() => UpdateStreamStatusBar());
        };
        StreamManager.Instance.ClientDisconnected += (s, e) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() => UpdateStreamStatusBar());
        };

    }

    private void UpdateSegueTimers(TimeSpan position, MusicTrack? track)
    {
        _lblIntroValue.Text = RemainingMarker(track?.Intro, position);
        _lblVoiceOutValue.Text = RemainingMarker(track?.VoiceOut, position);
        _lblMixInValue.Text = RemainingMarker(track?.MixIn, position);
    }

    private static string RemainingMarker(TimeSpan? marker, TimeSpan position)
    {
        if (marker == null || marker.Value <= TimeSpan.Zero) return "--";
        var remaining = marker.Value - position;
        return remaining > TimeSpan.Zero ? $"{Math.Ceiling(remaining.TotalSeconds):0}s" : "GO";
    }

    private void OnMainAggregatorChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old aggregator
        if (_currentAggregator != null)
        {
            _currentAggregator.MaximumCalculated -= OnMaxSample;
            _currentAggregator = null;
        }

        // Subscribe to new aggregator
        var agg = AudioEngine.Instance.MainSampleAggregator;
        _currentAggregator = agg;
        if (agg != null)
        {
            agg.MaximumCalculated += OnMaxSample;
        }
        else
        {
            if (IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    _vuLeft.UpdateLevel(0);
                    _vuRight.UpdateLevel(0);
                });
            }
        }
    }

    private void OnMaxSample(object? sender, MaxSampleEventArgs e)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            _vuLeft.UpdateLevel(e.LeftPeak);
            _vuRight.UpdateLevel(e.RightPeak);
        });
    }

    private void UpdateStreamStatusBar()
    {
        bool on = StreamManager.Instance.IsStreaming;
        bool onAir = on || AudioEngine.Instance.IsPlaying;
        _lblOnAir.Text = onAir ? "ON AIR" : "OFF AIR";
        _lblOnAir.BackColor = onAir ? Color.FromArgb(170, 20, 20) : Color.FromArgb(70, 20, 20);
        _lblOnAir.ForeColor = onAir ? Color.FromArgb(255, 245, 170) : Color.FromArgb(255, 180, 180);
        if (on)
        {
            int listeners = StreamManager.Instance.ListenerCount;
            string host = StreamManager.Instance.IsNetworkWide
                ? StreamManager.GetLocalIpAddress()
                : "localhost";
            string url = $"http://{host}:{StreamManager.Instance.Port}/stream";
            _lblStreamStatus.Text = $"🟢  Stream: ON  —  {listeners} listener{(listeners == 1 ? "" : "s")}  —  {url}";
            _lblStreamStatus.ForeColor = Color.FromArgb(100, 240, 100);
        }
        else
        {
            _lblStreamStatus.Text = "🔴  Stream: OFF  —  Click 'Stream Settings' to enable web streaming";
            _lblStreamStatus.ForeColor = Color.FromArgb(180, 180, 200);
        }
    }

    private void StudioForm_Load(object? sender, EventArgs e)
    {
        PopulateLibraryView();
        PopulatePlaylistCombo();
        LoadCurrentPlaylist();
        LoadSession();
        // If session restored a different playlist, refresh the view
        PopulatePlaylistDisplayFields();
        RefreshPlaylistView();
        ShowNextTrack();
        UpdateStreamStatusBar();
    }

    private void PopulateLibraryView()
    {
        _lvLibrary.BeginUpdate();
        _lvLibrary.Items.Clear();
        foreach (var track in MusicDb?.GetAll() ?? System.Linq.Enumerable.Empty<MusicTrack>())
        {
            var lvi = new ListViewItem(track.Artist);
            lvi.SubItems.Add(track.Title);
            lvi.SubItems.Add(FormatTime(track.Duration));
            lvi.Tag = track;
            _lvLibrary.Items.Add(lvi);
        }
        _lvLibrary.EndUpdate();
    }

    private void LoadCurrentPlaylist()
    {
        _currentPlaylist = PlaylistDb?.GetCurrentPlaylist();
        if (_currentPlaylist == null)
            _currentPlaylist = new Playlist { Name = "Empty Playlist" };

        // Sync combo selection
        foreach (var item in _cboPlaylistSelect.Items)
        {
            if (item is Playlist p && p.Id == _currentPlaylist.Id)
            {
                _cboPlaylistSelect.SelectedItem = item;
                break;
            }
        }

        PopulatePlaylistDisplayFields();
        RefreshPlaylistView();
        ShowNextTrack();
    }

    private void RefreshPlaylistView()
    {
        _lvPlaylist.BeginUpdate();
        _lvPlaylist.Items.Clear();
        if (_currentPlaylist == null) { _lvPlaylist.EndUpdate(); return; }

        TimeSpan running = TimeSpan.Zero;
        for (int i = 0; i < _currentPlaylist.Items.Count; i++)
        {
            var item = _currentPlaylist.Items[i];
            EnsureDisplayFields(item);
            var lvi = new ListViewItem((i + 1).ToString());
            lvi.SubItems.Add(item.Artist);
            lvi.SubItems.Add(item.Title);
            lvi.SubItems.Add(FormatTime(item.Duration ?? TimeSpan.Zero));
            lvi.SubItems.Add(running.ToString(@"hh\:mm\:ss"));
            lvi.SubItems.Add(item.IsPlayed ? "Played" : (i == _currentIndex + 1 ? "Next" : (i == _currentIndex ? "Playing" : "")));
            lvi.Tag = i;

            if (item.IsPlayed) lvi.BackColor = Win98Theme.ExpiredColor;
            else if (i == _currentIndex) lvi.BackColor = Win98Theme.PlayingColor;
            else if (i == _currentIndex + 1) lvi.BackColor = Win98Theme.WaitingColor;

            running += item.Duration ?? TimeSpan.Zero;
            _lvPlaylist.Items.Add(lvi);
        }
        _lvPlaylist.EndUpdate();
    }

    private void UpdatePlaylistStatus()
    {
        RefreshPlaylistView();
        ShowNextTrack();
    }

    private void ShowNextTrack()
    {
        if (_currentPlaylist == null || _currentPlaylist.Items.Count == 0) return;
        int nextIdx = _currentIndex + 1;
        if (nextIdx < _currentPlaylist.Items.Count)
        {
            var next = _currentPlaylist.Items[nextIdx];
            EnsureDisplayFields(next);
            _lblNextArtist.Text = next.Artist;
            _lblNextTitle.Text = next.Title;
            _lblNextDuration.Text = FormatTime(next.Duration ?? TimeSpan.Zero);
        }
        else
        {
            _lblNextArtist.Text = "--- End of Playlist ---";
            _lblNextTitle.Text = string.Empty;
            _lblNextDuration.Text = string.Empty;
        }
    }

    private void BtnPlayPause_Click(object? sender, EventArgs e)
    {
        if (AudioEngine.Instance.IsPlaying)
        {
            AudioEngine.Instance.Pause();
            _btnPlayPause.Text = "▶ PLAY";
            _btnPlayPause.BackColor = Color.FromArgb(200, 255, 200);
        }
        else if (AudioEngine.Instance.IsPaused)
        {
            AudioEngine.Instance.Resume();
            _btnPlayPause.Text = "⏸ PAUSE";
            _btnPlayPause.BackColor = Color.FromArgb(255, 255, 180);
        }
        else
        {
            PlayCurrent();
        }
    }

    private void PlayCurrent()
    {
        if ((_currentPlaylist == null || _currentPlaylist.Items.Count == 0) && _chkRadioAway.Checked)
            BuildRadioAwayPlaylist();

        if (_currentPlaylist == null || _currentPlaylist.Items.Count == 0) return;
        if (_currentIndex < 0) _currentIndex = 0;
        if (_currentIndex >= _currentPlaylist.Items.Count) return;

        var item = _currentPlaylist.Items[_currentIndex];
        EnsureDisplayFields(item);
        var track = MusicDb?.GetById(item.TrackId);
        if (track == null)
            track = new MusicTrack { Artist = item.Artist, Title = item.Title, FileName = string.Empty };

        if (!File.Exists(track.FullPath))
        {
            // Mark as played and auto-skip instead of blocking
            item.IsPlayed = true;
            RefreshPlaylistView();
            if (_autoPlay && _currentIndex + 1 < _currentPlaylist.Items.Count)
            {
                _currentIndex++;
                PlayCurrent();
            }
            else
            {
                MessageBox.Show($"File not found:\n{track.FullPath}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        item.ActualStart = DateTime.Now;
        AudioEngine.Instance.PlayTrack(track);
        MusicDb?.UpdatePlayStats(track.Id);
        LogDb?.AddEntry(track.Artist, track.Title, track.FileName, EventType.Music);
        RefreshPlaylistView();
    }

    private void EnsureDisplayFields(PlaylistItem item)
    {
        bool missingArtistTitle = string.IsNullOrWhiteSpace(item.Artist) || string.IsNullOrWhiteSpace(item.Title);
        bool missingDuration = !item.Duration.HasValue || item.Duration.Value <= TimeSpan.Zero;

        if (!missingArtistTitle && !missingDuration)
            return;

        var track = MusicDb?.GetById(item.TrackId);
        if (track == null) return;

        if (missingArtistTitle)
        {
            item.Artist = track.Artist;
            item.Title = track.Title;
        }

        if (missingDuration && track.Duration > TimeSpan.Zero)
            item.Duration = track.Duration;
    }

    private void PlayNext()
    {
        if (_currentPlaylist == null) return;
        _currentIndex++;
        if (_currentIndex >= _currentPlaylist.Items.Count)
        {
            if (_chkRadioAway.Checked && BuildRadioAwayPlaylist())
            {
                _currentIndex = 0;
                PlayCurrent();
                ShowNextTrack();
                return;
            }

            _currentIndex = _currentPlaylist.Items.Count - 1;
            return;
        }
        PlayCurrent();
        ShowNextTrack();
    }

    private bool BuildRadioAwayPlaylist()
    {
        var allTracks = (MusicDb?.GetAll() ?? Enumerable.Empty<MusicTrack>())
            .Where(t => !string.IsNullOrWhiteSpace(t.FileName))
            .ToList();

        if (allTracks.Count == 0)
            return false;

        var stingers = allTracks
            .Where(t => t.PlaylistName.Equals("Stinger", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var musicTracks = allTracks
            .Where(t => !t.PlaylistName.Equals("Stinger", StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => _random.Next())
            .ToList();

        if (musicTracks.Count == 0)
            musicTracks = allTracks.OrderBy(_ => _random.Next()).ToList();

        var away = new Playlist
        {
            Name = $"Radio Away {DateTime.Now:HH:mm}",
            IsAutomatic = true,
            CreatedDate = DateTime.Now
        };

        int order = 0;
        foreach (var track in musicTracks)
        {
            away.Items.Add(ToPlaylistItem(track, order++));

            if (stingers.Count > 0 && _random.Next(100) < AwayStingerChancePercent)
            {
                var stinger = stingers[_random.Next(stingers.Count)];
                away.Items.Add(ToPlaylistItem(stinger, order++));
            }
        }

        _currentPlaylist = away;
        _currentIndex = -1;
        RefreshPlaylistView();
        ShowNextTrack();
        return away.Items.Count > 0;
    }

    private static PlaylistItem ToPlaylistItem(MusicTrack track, int order) => new()
    {
        TrackId = track.Id,
        TrackType = TrackType.Music,
        Order = order,
        Duration = track.Duration,
        Artist = track.Artist,
        Title = track.Title
    };

    private void MarkCurrentPlayed()
    {
        if (_currentPlaylist == null || _currentIndex < 0 || _currentIndex >= _currentPlaylist.Items.Count) return;
        var item = _currentPlaylist.Items[_currentIndex];
        item.IsPlayed = true;
        item.ActualEnd = DateTime.Now;
    }

    private void BtnNext_Click(object? sender, EventArgs e)
    {
        MarkCurrentPlayed();
        _manualJump = true;
        AudioEngine.Instance.Stop();
        PlayNext();
    }

    private void BtnCue_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null || _currentIndex < 0) return;
        var item = _currentPlaylist.Items[_currentIndex];
        var track = MusicDb?.GetById(item.TrackId);
        if (track != null && track.HotStart > TimeSpan.Zero)
            AudioEngine.Instance.SetPosition(track.HotStart);
    }

    private void BtnLoadNext_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null) return;
        int nextIdx = _currentIndex + 1;
        if (nextIdx >= _currentPlaylist.Items.Count) return;
        var item = _currentPlaylist.Items[nextIdx];
        var track = MusicDb?.GetById(item.TrackId);
        if (track != null)
        {
            AudioEngine.Instance.QueueNext(track);
            _lblPreviewTrack.Text = $"Queued: {track.Artist} - {track.Title}";
        }
    }

    private void BtnPreviewPlay_Click(object? sender, EventArgs e)
    {
        // Play the "next" track on the preview device so the operator can listen before airing
        if (_currentPlaylist == null) return;
        int nextIdx = _currentIndex + 1;
        if (nextIdx >= _currentPlaylist.Items.Count) return;
        var item = _currentPlaylist.Items[nextIdx];
        var track = MusicDb?.GetById(item.TrackId);
        if (track == null || !File.Exists(track.FullPath))
        {
            MessageBox.Show("Next track file not found.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Preview);
        _lblPreviewTrack.Text = $"Preview: {track.Artist} — {track.Title}";
    }

    private void BtnMix2Ch_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null) return;
        int nextIdx = _currentIndex + 1;
        if (nextIdx >= _currentPlaylist.Items.Count)
        {
            MessageBox.Show("No next track available for 2-channel mix.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var nextItem = _currentPlaylist.Items[nextIdx];
        var nextTrack = MusicDb?.GetById(nextItem.TrackId);
        if (nextTrack == null || !File.Exists(nextTrack.FullPath))
        {
            MessageBox.Show("Next track file not found.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AudioEngine.Instance.PlayTrack(nextTrack, AudioDeviceType.Preview);
        _lblPreviewTrack.Text = $"2CH PreMix: {nextTrack.Artist} — {nextTrack.Title}";
        var swapTimer = new System.Windows.Forms.Timer { Interval = 3500 };
        swapTimer.Tick += (s, args) =>
        {
            swapTimer.Stop();
            swapTimer.Dispose();
            MarkCurrentPlayed();
            _manualJump = true;
            AudioEngine.Instance.FadeOut(AudioDeviceType.Main, TimeSpan.FromMilliseconds(800), () =>
            {
                if (!IsHandleCreated) return;
                BeginInvoke(() =>
                {
                    _currentIndex = nextIdx;
                    PlayCurrent();
                });
            });
        };
        swapTimer.Start();
    }

    private void LvLibrary_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is ListViewItem lvi && lvi.Tag is MusicTrack track)
            DoDragDrop(track, DragDropEffects.Copy);
    }

    private void LvPlaylist_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(MusicTrack)) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void LvPlaylist_DragDrop(object? sender, DragEventArgs e)
    {
        if (_currentPlaylist == null || e.Data?.GetData(typeof(MusicTrack)) is not MusicTrack track) return;
        var insertIndex = _lvPlaylist.PointToClient(new Point(e.X, e.Y));
        var target = _lvPlaylist.GetItemAt(insertIndex.X, insertIndex.Y);
        int idx = target?.Index ?? _currentPlaylist.Items.Count;
        _currentPlaylist.Items.Insert(Math.Clamp(idx, 0, _currentPlaylist.Items.Count), new PlaylistItem
        {
            TrackId = track.Id,
            Artist = track.Artist,
            Title = track.Title,
            Duration = track.Duration,
            TrackType = TrackType.Music
        });
        RefreshPlaylistView();
        ShowNextTrack();
    }

    private void BtnEditQueuePoints_Click(object? sender, EventArgs e)
    {
        if (_lvPlaylist.SelectedItems.Count == 0) return;
        var idx = (int)(_lvPlaylist.SelectedItems[0].Tag ?? -1);
        if (_currentPlaylist == null || idx < 0 || idx >= _currentPlaylist.Items.Count) return;
        var item = _currentPlaylist.Items[idx];
        var track = MusicDb?.GetById(item.TrackId);
        if (track == null) return;

        using var dlg = new QueuePointEditorForm(track);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            MusicDb?.Update(track);
    }

    private void BtnAddToLibrary_Click(object? sender, EventArgs e)
    {
        if (MusicDb == null)
        {
            MessageBox.Show("Music database not connected.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var dlg = new TrackEditorForm { MusicDb = MusicDb };
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Track != null)
        {
            try
            {
                MusicDb.Add(dlg.Track);
                PopulateLibraryView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add track:\n{ex.Message}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LvPlaylist_DoubleClick(object? sender, EventArgs e)
    {
        if (_lvPlaylist.SelectedItems.Count == 0) return;
        int idx = (int)(_lvPlaylist.SelectedItems[0].Tag ?? 0);
        MarkCurrentPlayed();
        _manualJump = true;
        AudioEngine.Instance.Stop();
        _currentIndex = idx;
        PlayCurrent();
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return ts.ToString(@"h\:mm\:ss");
        return ts.ToString(@"m\:ss");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveSession();
        _clockTimer.Stop();

        // Stop mic talkover if active
        // Safely unsubscribe
        AudioEngine.Instance.MainSampleAggregatorChanged -= OnMainAggregatorChanged;
        if (_currentAggregator != null)
        {
            _currentAggregator.MaximumCalculated -= OnMaxSample;
        }

        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Common radio-style hotkeys for quick on-air operation.
        if (keyData == Keys.Space) { BtnPlayPause_Click(this, EventArgs.Empty); return true; }
        if (keyData == (Keys.Control | Keys.Right)) { BtnNext_Click(this, EventArgs.Empty); return true; }
        if (keyData == (Keys.Control | Keys.M)) { BtnMix2Ch_Click(this, EventArgs.Empty); return true; }
        if (keyData == (Keys.Control | Keys.Shift | Keys.P)) { BtnPanic_Click(this, EventArgs.Empty); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ── PANIC ────────────────────────────────────────────────────────────────

    private void BtnPanic_Click(object? sender, EventArgs e)
    {
        var res = MessageBox.Show(
            "PANIC: Stop all audio immediately and reset the playback engine?\n\n" +
            "Use this if audio is stuck, distorted, or otherwise unrecoverable.",
            "PANIC – Safe Restart",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (res != DialogResult.Yes) return;

        try
        {
            // Stop all audio
            AudioEngine.Instance.Stop(AudioDeviceType.Main);
            AudioEngine.Instance.Stop(AudioDeviceType.Preview);

            // Dispose and recreate audio engine resources
            AudioEngine.Instance.Dispose();

            // Reset UI state
            _btnPlayPause.Text = "▶ PLAY";
            _btnPlayPause.BackColor = Color.FromArgb(200, 255, 200);
            _progressBar.Value = 0;
            _lblElapsed.Text = "0:00";
            _lblRemaining.Text = "-0:00";
            _lblArtist.Text = "--- PANIC RESET ---";
            _lblTitle.Text = "Audio engine restarted";
            _lblOnAir.Text = "OFF AIR";
            _lblOnAir.BackColor = Color.FromArgb(70, 20, 20);

            // Reconnect engine events
            ConnectAudioEngine();

            MessageBox.Show("Audio engine has been safely restarted.", "PANIC – Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ErrorLog.Instance.Add("StudioForm.Panic", ex.Message);
            MessageBox.Show($"Panic reset encountered an error:\n{ex.Message}",
                "PANIC", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── REMINDER ─────────────────────────────────────────────────────────────

    private void BtnReminder_Click(object? sender, EventArgs e)
    {
        using var dlg = new ReminderDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ReminderEntry != null)
        {
            _reminders.Enqueue(dlg.ReminderEntry);
            MessageBox.Show($"Reminder set for {dlg.ReminderEntry.TriggerTime:HH:mm}: {dlg.ReminderEntry.Message}",
                "Reminder Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void CheckReminders()
    {
        if (_reminders.Count == 0) return;
        var now = DateTime.Now;
        while (_reminders.Count > 0 && _reminders.Peek().TriggerTime <= now)
        {
            var r = _reminders.Dequeue();
            ShowReminderAlert(r);
        }
    }

    private void ShowReminderAlert(ReminderEntry r)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            _lblReminder.Text = $"  🔔 {r.Message}";
            _lblReminder.Size = new Size(400, 20);
            _lblReminder.Visible = true;

            // Auto-hide after 30 seconds using a one-shot timer
            var hideTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            EventHandler? tickHandler = null;
            tickHandler = (s2, e2) =>
            {
                _lblReminder.Visible = false;
                hideTimer.Stop();
                hideTimer.Tick -= tickHandler;
                hideTimer.Dispose();
            };
            hideTimer.Tick += tickHandler;
            hideTimer.Start();

            MessageBox.Show($"REMINDER:\n\n{r.Message}", "GoonNet Reminder",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }
}

// ── Reminder support types ────────────────────────────────────────────────────

public class ReminderEntry
{
    public DateTime TriggerTime { get; set; }
    public string Message { get; set; } = string.Empty;
}

internal class ReminderDialog : Form
{
    public ReminderEntry? ReminderEntry { get; private set; }

    private DateTimePicker _dtpTime = null!;
    private TextBox _txtMessage = null!;

    public ReminderDialog()
    {
        Text = "Set Reminder";
        Size = new Size(380, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;

        Controls.Add(new Label { Text = "Reminder time:", Location = new Point(10, 14), Size = new Size(100, 18) });
        _dtpTime = new DateTimePicker { Location = new Point(116, 10), Size = new Size(230, 22), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm  dd/MM/yyyy" };
        _dtpTime.Value = DateTime.Now.AddMinutes(15);
        Controls.Add(_dtpTime);

        Controls.Add(new Label { Text = "Message:", Location = new Point(10, 46), Size = new Size(100, 18) });
        _txtMessage = new TextBox { Location = new Point(116, 42), Size = new Size(230, 60), Multiline = true };
        Controls.Add(_txtMessage);

        var btnOk = new Button { Text = "Set Reminder", DialogResult = DialogResult.OK, Location = new Point(80, 118), Size = new Size(110, 28), FlatStyle = FlatStyle.System };
        btnOk.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_txtMessage.Text)) { DialogResult = DialogResult.None; return; }
            ReminderEntry = new ReminderEntry { TriggerTime = _dtpTime.Value, Message = _txtMessage.Text.Trim() };
        };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(202, 118), Size = new Size(80, 28), FlatStyle = FlatStyle.System };
        Controls.Add(btnOk); Controls.Add(btnCancel);
        AcceptButton = btnOk; CancelButton = btnCancel;
    }
}
