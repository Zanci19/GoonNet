using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

public class StudioForm : Form
{
    public PlaylistDatabase PlaylistDb { get; set; } = null!;
    public MusicDatabase MusicDb { get; set; } = null!;
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
    private VUMeter _vuMeter = null!;

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

    // Reminder
    private System.Windows.Forms.Timer _reminderTimer = null!;
    private Label _lblReminder = null!;
    private System.Collections.Generic.Queue<ReminderEntry> _reminders = new();

    // Up Next panel
    private Label _lblNextArtist = null!;
    private Label _lblNextTitle = null!;
    private Label _lblNextDuration = null!;
    private Button _btnLoadNext = null!;

    // Playlist list view
    private ListView _lvPlaylist = null!;

    // Preview section
    private Label _lblPreviewTrack = null!;
    private Button _btnPreviewPlay = null!;
    private Button _btnPreviewStop = null!;
    private TrackBar _previewVolumeSlider = null!;

    // Spectrum analyzer
    private SpectrumAnalyzer _spectrum = null!;

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

    // Studio Tools bar (mic + transition)
    private Panel _studioToolsBar = null!;
    // -- Mic talkover
    private Button _btnTalkover = null!;
    private ProgressBar _micLevelBar = null!;
    private TrackBar _duckingSlider = null!;
    private ComboBox _cboMicDevice = null!;
    // -- Transition sounds
    private TextBox _txtTransitionSound = null!;
    private NumericUpDown _nudFadeSecs = null!;
    private NumericUpDown _nudFadeInSecs = null!;
    private Button _btnFadeAndNext = null!;

    // Transition playback state
    private bool _playingTransition;
    private MusicTrack? _transitionTrack;
    private TimeSpan? _pendingTransitionFadeIn;

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
        Text = "Studio";
        Size = new Size(1060, 810);
        MinimumSize = new Size(860, 700);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);
        KeyPreview = true;

        // ══════════════════════════════════════════════════════════════════════
        // TOP CONTROLS PANEL (fixed height, stretches horizontally)
        // ══════════════════════════════════════════════════════════════════════
        var topPanel = new Panel
        {
            Location = new Point(0, 0),
            Height = 196,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = SystemColors.Control
        };
        topPanel.SizeChanged += (s, e) => topPanel.Width = ClientSize.Width;

        // ---- Clock (top-right of form) ----
        _lblClock = new Label
        {
            Text = DateTime.Now.ToString("HH:mm:ss"),
            Font = new Font("Microsoft Sans Serif", 22f, FontStyle.Bold),
            ForeColor = Color.Navy,
            Size = new Size(200, 42),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        // ---- NOW PLAYING PANEL ----
        var nowPanel = new GroupBox
        {
            Text = "NOW PLAYING",
            Location = new Point(8, 4),
            Size = new Size(360, 186),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };

        _lblArtist = new Label
        {
            Text = "--- No Track Loaded ---",
            Font = new Font("Microsoft Sans Serif", 11f, FontStyle.Bold),
            ForeColor = Color.Navy,
            Location = new Point(6, 18),
            Size = new Size(348, 22),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _lblTitle = new Label
        {
            Text = string.Empty,
            Font = new Font("Microsoft Sans Serif", 9f),
            Location = new Point(6, 42),
            Size = new Size(348, 18),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _progressBar = new ProgressBar
        {
            Location = new Point(6, 68),
            Size = new Size(288, 16),
            Minimum = 0,
            Maximum = 1000,
            Style = ProgressBarStyle.Continuous,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _lblElapsed = new Label
        {
            Text = "0:00",
            Location = new Point(6, 88),
            Size = new Size(80, 16),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        _lblRemaining = new Label
        {
            Text = "-0:00",
            Location = new Point(220, 88),
            Size = new Size(80, 16),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Microsoft Sans Serif", 8f)
        };

        _vuMeter = new VUMeter
        {
            Location = new Point(302, 64),
            Size = new Size(52, 112),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        var lblIntro = new Label { Text = "Intro:", Location = new Point(6, 114), Size = new Size(40, 13), Font = new Font("Microsoft Sans Serif", 7f) };
        _lblIntroValue = new Label { Text = "--", Location = new Point(44, 114), Size = new Size(30, 13), Font = new Font("Microsoft Sans Serif", 7f, FontStyle.Bold), ForeColor = Color.DarkGreen };
        var lblVoiceOut = new Label { Text = "VoiceOut:", Location = new Point(78, 114), Size = new Size(55, 13), Font = new Font("Microsoft Sans Serif", 7f) };
        _lblVoiceOutValue = new Label { Text = "--", Location = new Point(132, 114), Size = new Size(30, 13), Font = new Font("Microsoft Sans Serif", 7f, FontStyle.Bold), ForeColor = Color.Maroon };
        var lblMixIn = new Label { Text = "MixIn:", Location = new Point(165, 114), Size = new Size(40, 13), Font = new Font("Microsoft Sans Serif", 7f) };
        _lblMixInValue = new Label { Text = "--", Location = new Point(206, 114), Size = new Size(30, 13), Font = new Font("Microsoft Sans Serif", 7f, FontStyle.Bold), ForeColor = Color.Navy };

        nowPanel.Controls.AddRange(new Control[] { _lblArtist, _lblTitle, _progressBar, _lblElapsed, _lblRemaining, _vuMeter, lblIntro, _lblIntroValue, lblVoiceOut, _lblVoiceOutValue, lblMixIn, _lblMixInValue });

        // ---- PLAYBACK CONTROLS ----
        var ctrlPanel = new GroupBox
        {
            Text = "CONTROLS",
            Location = new Point(376, 4),
            Size = new Size(222, 196),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };

        _btnPlayPause = new Button
        {
            Text = "▶ PLAY",
            Location = new Point(8, 20),
            Size = new Size(100, 36),
            BackColor = Color.FromArgb(200, 255, 200),
            FlatStyle = FlatStyle.System,
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold)
        };
        _btnPlayPause.Click += BtnPlayPause_Click;

        _btnStop = new Button
        {
            Text = "■ STOP",
            Location = new Point(112, 20),
            Size = new Size(96, 36),
            BackColor = Color.FromArgb(255, 200, 200),
            FlatStyle = FlatStyle.System,
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold)
        };
        _btnStop.Click += (s, e) => { AudioEngine.Instance.Stop(); UpdatePlaylistStatus(); };

        _btnNext = new Button { Text = "⏭ NEXT", Location = new Point(8, 62), Size = new Size(96, 28), FlatStyle = FlatStyle.System };
        _btnNext.Click += BtnNext_Click;

        _btnCue = new Button { Text = "CUE", Location = new Point(112, 62), Size = new Size(96, 28), FlatStyle = FlatStyle.System };
        _btnCue.Click += BtnCue_Click;

        _btnFadeOut = new Button { Text = "FADE OUT", Location = new Point(8, 96), Size = new Size(200, 24), FlatStyle = FlatStyle.System };
        _btnFadeOut.Click += (s, e) => AudioEngine.Instance.FadeOut(AudioDeviceType.Main, TimeSpan.FromSeconds(5));

        _btnPanic = new Button
        {
            Text = "⚡ PANIC",
            Location = new Point(8, 126),
            Size = new Size(200, 24),
            BackColor = Color.FromArgb(200, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.System,
            Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold)
        };
        _btnPanic.Click += BtnPanic_Click;

        var lblVol = new Label { Text = "Volume:", Location = new Point(8, 156), Size = new Size(52, 16) };
        _volumeSlider = new TrackBar
        {
            Location = new Point(62, 150),
            Size = new Size(118, 30),
            Minimum = 0,
            Maximum = 100,
            Value = 85,
            TickFrequency = 10
        };
        _volumeSlider.ValueChanged += (s, e) =>
        {
            AudioEngine.Instance.MainVolume = _volumeSlider.Value / 100f;
            _lblVolume.Text = _volumeSlider.Value + "%";
        };
        _lblVolume = new Label { Text = "85%", Location = new Point(183, 156), Size = new Size(30, 16) };

        _chkAutoPlay = new CheckBox { Text = "Auto-Play", Location = new Point(8, 180), Checked = true };
        _chkAutoPlay.CheckedChanged += (s, e) => _autoPlay = _chkAutoPlay.Checked;

        var btnReminder = new Button { Text = "🔔 Reminder...", Location = new Point(100, 180), Size = new Size(112, 18), FlatStyle = FlatStyle.System, Font = new Font("Microsoft Sans Serif", 7f) };
        btnReminder.Click += BtnReminder_Click;

        ctrlPanel.Controls.AddRange(new Control[] { _btnPlayPause, _btnStop, _btnNext, _btnCue, _btnFadeOut, _btnPanic, lblVol, _volumeSlider, _lblVolume, _chkAutoPlay, btnReminder });

        // Reminder display label (in the top panel, below ctrlPanel area)
        _lblReminder = new Label
        {
            Location = new Point(376, 196),
            Size = new Size(400, 0),
            ForeColor = Color.FromArgb(255, 220, 80),
            BackColor = Color.FromArgb(60, 50, 10),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        topPanel.Controls.Add(_lblReminder);

        // ---- RIGHT PANEL (UP NEXT + PREVIEW + CLOCK, grows horizontally) ----
        var rightPanel = new Panel
        {
            Location = new Point(606, 4),
            Size = new Size(440, 186),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = SystemColors.Control
        };

        // Position clock at top-right of rightPanel
        _lblClock.Location = new Point(rightPanel.Width - 208, 0);
        _lblClock.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _lblOnAir = new Label
        {
            Text = "OFF AIR",
            Location = new Point(238, 10),
            Size = new Size(96, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(70, 20, 20),
            ForeColor = Color.FromArgb(255, 180, 180),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle
        };

        var nextPanel = new GroupBox
        {
            Text = "UP NEXT",
            Location = new Point(0, 0),
            Size = new Size(440, 100),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        _lblNextArtist = new Label { Text = "---", Location = new Point(6, 18), Size = new Size(378, 18), Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _lblNextTitle = new Label { Text = string.Empty, Location = new Point(6, 38), Size = new Size(378, 16), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _lblNextDuration = new Label { Text = string.Empty, Location = new Point(6, 58), Size = new Size(150, 14), Font = new Font("Microsoft Sans Serif", 7f) };
        _btnLoadNext = new Button { Text = "Load Next", Location = new Point(158, 54), Size = new Size(80, 22), FlatStyle = FlatStyle.System };
        _btnLoadNext.Click += BtnLoadNext_Click;
        nextPanel.Controls.AddRange(new Control[] { _lblNextArtist, _lblNextTitle, _lblNextDuration, _btnLoadNext });

        var previewPanel = new GroupBox
        {
            Text = "PREVIEW / MONITOR",
            Location = new Point(0, 108),
            Size = new Size(440, 74),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        _lblPreviewTrack = new Label { Text = "No preview loaded", Location = new Point(6, 18), Size = new Size(380, 14), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _btnPreviewPlay = new Button { Text = "▶", Location = new Point(6, 36), Size = new Size(36, 24), FlatStyle = FlatStyle.System };
        _btnPreviewPlay.Click += BtnPreviewPlay_Click;
        _btnPreviewStop = new Button { Text = "■", Location = new Point(46, 36), Size = new Size(36, 24), FlatStyle = FlatStyle.System };
        _btnPreviewStop.Click += (s, e) => AudioEngine.Instance.Stop(AudioDeviceType.Preview);
        var lblPreviewVol = new Label { Text = "Vol:", Location = new Point(90, 40), Size = new Size(28, 16) };
        _previewVolumeSlider = new TrackBar { Location = new Point(118, 34), Size = new Size(110, 28), Minimum = 0, Maximum = 100, Value = 80, TickFrequency = 20 };
        _previewVolumeSlider.ValueChanged += (s, e) => AudioEngine.Instance.PreviewVolume = _previewVolumeSlider.Value / 100f;
        previewPanel.Controls.AddRange(new Control[] { _lblPreviewTrack, _btnPreviewPlay, _btnPreviewStop, lblPreviewVol, _previewVolumeSlider });

        rightPanel.Controls.AddRange(new Control[] { _lblClock, _lblOnAir, nextPanel, previewPanel });
        topPanel.Controls.AddRange(new Control[] { nowPanel, ctrlPanel, rightPanel });

        // ══════════════════════════════════════════════════════════════════════
        // STREAMING STATUS BAR
        // ══════════════════════════════════════════════════════════════════════
        var streamBar = new Panel
        {
            Location = new Point(0, 196),
            Height = 28,
            BackColor = Color.FromArgb(30, 30, 45),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        streamBar.SizeChanged += (s, e) => streamBar.Width = ClientSize.Width;

        _lblStreamStatus = new Label
        {
            Text = "🔴  Stream: OFF  —  Click 'Stream Settings' to enable web streaming",
            ForeColor = Color.FromArgb(180, 180, 200),
            Font = new Font("Microsoft Sans Serif", 8f),
            Location = new Point(6, 5),
            Size = new Size(750, 18),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _btnStreamSettings = new Button
        {
            Text = "📡 Stream Settings",
            Location = new Point(streamBar.Width - 130, 3),
            Size = new Size(126, 22),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 220, 255),
            BackColor = Color.FromArgb(50, 70, 100),
            Font = new Font("Microsoft Sans Serif", 7.5f),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnStreamSettings.FlatAppearance.BorderColor = Color.FromArgb(80, 110, 160);
        _btnStreamSettings.Click += (s, e) => OpenStreamingForm();
        streamBar.Controls.AddRange(new Control[] { _lblStreamStatus, _btnStreamSettings });

        // ══════════════════════════════════════════════════════════════════════
        // PITCH & SPEED BAR
        // ══════════════════════════════════════════════════════════════════════
        _pitchSpeedBar = BuildPitchSpeedBar();
        _pitchSpeedBar.Location = new Point(0, 224);
        _pitchSpeedBar.SizeChanged += (s, e) => _pitchSpeedBar.Width = ClientSize.Width;

        // ══════════════════════════════════════════════════════════════════════
        // STUDIO TOOLS BAR  (transition sounds + microphone talkover)
        // ══════════════════════════════════════════════════════════════════════
        _studioToolsBar = BuildStudioToolsBar();
        _studioToolsBar.Location = new Point(0, 288);
        _studioToolsBar.SizeChanged += (s, e) => _studioToolsBar.Width = ClientSize.Width;

        // ══════════════════════════════════════════════════════════════════════
        // LARGE SPECTRUM ANALYZER
        // ══════════════════════════════════════════════════════════════════════
        var spectrumPanel = new GroupBox
        {
            Text = "SPECTRUM ANALYZER",
            Location = new Point(0, 368),   // 288 + 80 toolsBar height
            Height = 280,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            BackColor = Color.Black,
            ForeColor = Color.FromArgb(0, 200, 0)
        };
        spectrumPanel.SizeChanged += (s, e) => spectrumPanel.Width = ClientSize.Width;

        _spectrum = new SpectrumAnalyzer
        {
            Location = new Point(4, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        spectrumPanel.Controls.Add(_spectrum);
        spectrumPanel.SizeChanged += (s, e) =>
        {
            _spectrum.Size = new Size(spectrumPanel.ClientSize.Width - 8, spectrumPanel.ClientSize.Height - 20);
        };

        // ══════════════════════════════════════════════════════════════════════
        // PLAYLIST PANEL (fills remaining space)
        // ══════════════════════════════════════════════════════════════════════
        var playlistPanel = new GroupBox
        {
            Text = "CURRENT PLAYLIST",
            Location = new Point(0, 648),   // 368 + 280 = 648
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };

        _lvPlaylist = new ListView
        {
            Location = new Point(4, 18),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _lvPlaylist.Columns.Add("#", 32);
        _lvPlaylist.Columns.Add("Artist", 170);
        _lvPlaylist.Columns.Add("Title", 210);
        _lvPlaylist.Columns.Add("Duration", 72);
        _lvPlaylist.Columns.Add("Start", 82);
        _lvPlaylist.Columns.Add("Status", 80);
        _lvPlaylist.DoubleClick += LvPlaylist_DoubleClick;

        playlistPanel.Controls.Add(_lvPlaylist);

        // Wire up SizeChanged to keep all panels filling the space
        SizeChanged += (s, e) =>
        {
            topPanel.Width = ClientSize.Width;
            streamBar.Width = ClientSize.Width;
            _pitchSpeedBar.Width = ClientSize.Width;
            _studioToolsBar.Width = ClientSize.Width;
            spectrumPanel.Width = ClientSize.Width;
            rightPanel.Width = ClientSize.Width - 614;

            playlistPanel.Location = new Point(0, 648);
            playlistPanel.Size = new Size(ClientSize.Width, ClientSize.Height - 648);
            _lvPlaylist.Size = new Size(playlistPanel.ClientSize.Width - 8, playlistPanel.ClientSize.Height - 22);

            _btnStreamSettings.Location = new Point(streamBar.Width - 130, 3);
        };

        Controls.AddRange(new Control[] { topPanel, streamBar, _pitchSpeedBar, _studioToolsBar, spectrumPanel, playlistPanel });

        // Fire SizeChanged once to initialize sizes
        OnSizeChanged(EventArgs.Empty);
    }

    private Panel BuildPitchSpeedBar()
    {
        var bar = new Panel
        {
            Height = 64,
            BackColor = Color.FromArgb(20, 22, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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
        int sliderX = 448;

        var lblPitch = new Label { Text = "Pitch:", Location = new Point(sliderX, 8), Size = new Size(34, 16), ForeColor = Color.FromArgb(180, 200, 255), Font = new Font("Microsoft Sans Serif", 7.5f, FontStyle.Bold) };
        _pitchSlider = new TrackBar
        {
            Location = new Point(sliderX + 36, 4),
            Size = new Size(120, 28),
            Minimum = -24,
            Maximum = 24,
            Value = 0,
            TickFrequency = 6,
            TickStyle = TickStyle.None,
            BackColor = Color.FromArgb(20, 22, 34)
        };
        _pitchSlider.ValueChanged += PitchSlider_Changed;
        _lblPitchVal = new Label { Text = "0 st", Location = new Point(sliderX + 158, 8), Size = new Size(38, 16), ForeColor = Color.Silver, Font = new Font("Microsoft Sans Serif", 7.5f) };

        sliderX += 204;
        var lblTempo = new Label { Text = "Tempo:", Location = new Point(sliderX, 8), Size = new Size(44, 16), ForeColor = Color.FromArgb(255, 210, 130), Font = new Font("Microsoft Sans Serif", 7.5f, FontStyle.Bold) };
        _tempoSlider = new TrackBar
        {
            Location = new Point(sliderX + 46, 4),
            Size = new Size(120, 28),
            Minimum = -50,
            Maximum = 100,
            Value = 0,
            TickFrequency = 25,
            TickStyle = TickStyle.None,
            BackColor = Color.FromArgb(20, 22, 34)
        };
        _tempoSlider.ValueChanged += TempoSlider_Changed;
        _lblTempoVal = new Label { Text = "0%", Location = new Point(sliderX + 168, 8), Size = new Size(38, 16), ForeColor = Color.Silver, Font = new Font("Microsoft Sans Serif", 7.5f) };

        sliderX += 210;
        var lblRate = new Label { Text = "Rate:", Location = new Point(sliderX, 8), Size = new Size(34, 16), ForeColor = Color.FromArgb(200, 255, 200), Font = new Font("Microsoft Sans Serif", 7.5f, FontStyle.Bold) };
        _rateSlider = new TrackBar
        {
            Location = new Point(sliderX + 36, 4),
            Size = new Size(120, 28),
            Minimum = -50,
            Maximum = 100,
            Value = 0,
            TickFrequency = 25,
            TickStyle = TickStyle.None,
            BackColor = Color.FromArgb(20, 22, 34)
        };
        _rateSlider.ValueChanged += RateSlider_Changed;
        _lblRateVal = new Label { Text = "x1.00", Location = new Point(sliderX + 158, 8), Size = new Size(44, 16), ForeColor = Color.Silver, Font = new Font("Microsoft Sans Serif", 7.5f) };

        sliderX += 210;
        var btnReset = new Button
        {
            Text = "↺ Reset",
            Location = new Point(sliderX, 4),
            Size = new Size(62, 22),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Silver,
            BackColor = Color.FromArgb(40, 40, 55),
            Font = new Font("Microsoft Sans Serif", 7.5f)
        };
        btnReset.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 100);
        btnReset.Click += (s, e) => ResetPitchSpeed();

        // ── SECOND ROW: tip labels ─────────────────────────────────────────
        var tipPitch = new Label { Text = "Pitch only (speed unchanged)", Location = new Point(484, 36), Size = new Size(184, 14), ForeColor = Color.FromArgb(90, 100, 130), Font = new Font("Microsoft Sans Serif", 6.5f) };
        var tipTempo = new Label { Text = "Speed only (pitch unchanged)", Location = new Point(688, 36), Size = new Size(184, 14), ForeColor = Color.FromArgb(90, 100, 130), Font = new Font("Microsoft Sans Serif", 6.5f) };
        var tipRate = new Label { Text = "Both pitch+speed (like tape)", Location = new Point(892, 36), Size = new Size(184, 14), ForeColor = Color.FromArgb(90, 100, 130), Font = new Font("Microsoft Sans Serif", 6.5f) };

        bar.Controls.AddRange(new Control[] {
            lblPl, _cboPlaylistSelect, btnLoad, _btnSaveSession,
            lblPitch, _pitchSlider, _lblPitchVal,
            lblTempo, _tempoSlider, _lblTempoVal,
            lblRate, _rateSlider, _lblRateVal,
            btnReset, tipPitch, tipTempo, tipRate
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
                AutoPlay = _autoPlay,
                TransitionSoundPath = _txtTransitionSound.Text,
                FadeSeconds = (int)_nudFadeSecs.Value,
                MicDeviceIndex = Math.Max(0, _cboMicDevice.SelectedIndex),
                DuckingPercent = _duckingSlider.Value
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

            if (!string.IsNullOrEmpty(session.TransitionSoundPath))
                _txtTransitionSound.Text = session.TransitionSoundPath;

            _nudFadeSecs.Value = Math.Clamp(session.FadeSeconds, (int)_nudFadeSecs.Minimum, (int)_nudFadeSecs.Maximum);

            if (session.MicDeviceIndex >= 0 && session.MicDeviceIndex < _cboMicDevice.Items.Count)
                _cboMicDevice.SelectedIndex = session.MicDeviceIndex;

            _duckingSlider.Value = Math.Clamp(session.DuckingPercent, 0, 100);

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

    private Panel BuildStudioToolsBar()
    {
        var bar = new Panel
        {
            Height = 80,
            BackColor = Color.FromArgb(25, 28, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // ── LEFT: Transition sound controls ────────────────────────────────
        var transGroup = new GroupBox
        {
            Text = "TRANSITION SOUND",
            Location = new Point(4, 4),
            Size = new Size(550, 72),
            Font = new Font("Microsoft Sans Serif", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 200, 255),
            BackColor = Color.FromArgb(25, 28, 38)
        };

        var lblFade = new Label { Text = "Fade:", Location = new Point(8, 22), Size = new Size(36, 16), ForeColor = Color.Silver };
        _nudFadeSecs = new NumericUpDown
        {
            Location = new Point(46, 20),
            Size = new Size(52, 20),
            Minimum = 1,
            Maximum = 30,
            Value = 5,
            DecimalPlaces = 0
        };
        var lblSecs = new Label { Text = "sec", Location = new Point(101, 22), Size = new Size(26, 16), ForeColor = Color.Silver };

        var lblSnd = new Label { Text = "Stinger:", Location = new Point(134, 22), Size = new Size(46, 16), ForeColor = Color.Silver };
        _txtTransitionSound = new TextBox
        {
            Location = new Point(182, 19),
            Size = new Size(220, 20),
            BackColor = Color.FromArgb(40, 45, 60),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "(optional .mp3/.wav stinger)"
        };
        var btnBrowse = new Button
        {
            Text = "📂",
            Location = new Point(406, 18),
            Size = new Size(30, 22),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Silver,
            BackColor = Color.FromArgb(50, 55, 70)
        };
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(80, 90, 110);
        btnBrowse.Click += BtnBrowseTransition_Click;

        _btnFadeAndNext = new Button
        {
            Text = "FADE & NEXT ▶",
            Location = new Point(442, 16),
            Size = new Size(102, 26),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 220, 100),
            BackColor = Color.FromArgb(80, 60, 10)
        };
        _btnFadeAndNext.FlatAppearance.BorderColor = Color.FromArgb(180, 140, 40);
        _btnFadeAndNext.Click += BtnFadeAndNext_Click;

        var lblFadeIn = new Label { Text = "Fade in:", Location = new Point(8, 48), Size = new Size(42, 16), ForeColor = Color.Silver };
        _nudFadeInSecs = new NumericUpDown
        {
            Location = new Point(54, 46),
            Size = new Size(52, 20),
            Minimum = 0,
            Maximum = 15,
            Value = 2,
            DecimalPlaces = 0
        };
        var lblFadeInSecs = new Label { Text = "sec", Location = new Point(110, 48), Size = new Size(26, 16), ForeColor = Color.Silver };

        var lblTransTip = new Label
        {
            Text = "Fades out current track, plays stinger, then fades in next track for smoother segues.",
            Location = new Point(136, 48),
            Size = new Size(404, 18),
            ForeColor = Color.FromArgb(120, 130, 150),
            Font = new Font("Microsoft Sans Serif", 7f)
        };

        transGroup.Controls.AddRange(new Control[] { lblFade, _nudFadeSecs, lblSecs, lblSnd, _txtTransitionSound, btnBrowse, _btnFadeAndNext, lblFadeIn, _nudFadeInSecs, lblFadeInSecs, lblTransTip });

        // ── RIGHT: Microphone talkover controls ────────────────────────────
        var micGroup = new GroupBox
        {
            Text = "🎤 MIC TALKOVER",
            Location = new Point(558, 4),
            Size = new Size(440, 72),
            Font = new Font("Microsoft Sans Serif", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 180, 180),
            BackColor = Color.FromArgb(25, 28, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        bar.SizeChanged += (s, e) => micGroup.Width = bar.Width - 562;

        var lblDev = new Label { Text = "Device:", Location = new Point(8, 20), Size = new Size(45, 16), ForeColor = Color.Silver };
        _cboMicDevice = new ComboBox
        {
            Location = new Point(56, 18),
            Size = new Size(200, 20),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(40, 45, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Popup
        };
        PopulateMicDevices();

        var lblDuck = new Label { Text = "Duck:", Location = new Point(262, 20), Size = new Size(36, 16), ForeColor = Color.Silver };
        _duckingSlider = new TrackBar
        {
            Location = new Point(300, 14),
            Size = new Size(80, 28),
            Minimum = 0,
            Maximum = 100,
            Value = 25,
            TickFrequency = 25,
            TickStyle = TickStyle.None
        };
        var lblDuckVal = new Label { Text = "25%", Location = new Point(384, 20), Size = new Size(32, 16), ForeColor = Color.Silver };
        _duckingSlider.ValueChanged += (s, e) =>
        {
            lblDuckVal.Text = _duckingSlider.Value + "%";
            MicrophoneManager.Instance.DuckLevel = _duckingSlider.Value / 100f;
        };

        _btnTalkover = new Button
        {
            Text = "⏵ TALKOVER",
            Location = new Point(8, 42),
            Size = new Size(250, 24),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 30, 30)
        };
        _btnTalkover.FlatAppearance.BorderColor = Color.FromArgb(150, 60, 60);
        _btnTalkover.Click += BtnTalkover_Click;

        _micLevelBar = new ProgressBar
        {
            Location = new Point(264, 42),
            Size = new Size(160, 18),
            Minimum = 0,
            Maximum = 100,
            Style = ProgressBarStyle.Continuous,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        bar.SizeChanged += (s, e) => _micLevelBar.Width = Math.Max(80, micGroup.Width - 270);

        micGroup.Controls.AddRange(new Control[] { lblDev, _cboMicDevice, lblDuck, _duckingSlider, lblDuckVal, _btnTalkover, _micLevelBar });

        bar.Controls.AddRange(new Control[] { transGroup, micGroup });
        return bar;
    }

    private void PopulateMicDevices()
    {
        _cboMicDevice.Items.Clear();
        int count = MicrophoneManager.DeviceCount;
        if (count == 0)
        {
            _cboMicDevice.Items.Add("(no microphone found)");
            // Leave SelectedIndex at -1 so the placeholder is not mistaken for a valid device
        }
        else
        {
            for (int i = 0; i < count; i++)
                _cboMicDevice.Items.Add(MicrophoneManager.GetDeviceName(i));
            _cboMicDevice.SelectedIndex = 0;
        }
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

                if (_playingTransition)
                {
                    // The transition stinger just finished — advance playlist normally
                    _playingTransition = false;
                    _transitionTrack = null;
                    if (_autoPlay)
                    {
                        PlayNext();
                        ApplyPendingTransitionFadeIn();
                    }
                }
                else if (_transitionTrack != null)
                {
                    // Current track faded out; now play the stinger
                    var stinger = _transitionTrack;
                    _transitionTrack = null;
                    _playingTransition = true;
                    if (File.Exists(stinger.FullPath))
                    {
                        AudioEngine.Instance.PlayTrack(stinger);
                        _lblArtist.Text = "🎵 " + stinger.Artist;
                        _lblTitle.Text = stinger.Title;
                    }
                    else
                    {
                        // Stinger file missing — skip straight to next
                        _playingTransition = false;
                        if (_autoPlay)
                        {
                            PlayNext();
                            ApplyPendingTransitionFadeIn();
                        }
                    }
                }
                else
                {
                    _pendingTransitionFadeIn = null;
                    if (_autoPlay) PlayNext();
                }
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

        // Mic level updates
        MicrophoneManager.Instance.LevelChanged += (s, level) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() => _micLevelBar.Value = (int)(level * 100));
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
            _currentAggregator.FftDataAvailable -= OnFftData;
            _currentAggregator.MaximumCalculated -= OnMaxSample;
            _currentAggregator = null;
        }

        // Subscribe to new aggregator
        var agg = AudioEngine.Instance.MainSampleAggregator;
        _currentAggregator = agg;
        if (agg != null)
        {
            agg.FftDataAvailable += OnFftData;
            agg.MaximumCalculated += OnMaxSample;
        }
        else
        {
            // Playback stopped — reset spectrum
            if (IsHandleCreated) BeginInvoke(() => _spectrum.Reset());
        }
    }

    private void OnFftData(object? sender, FftEventArgs e)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() => _spectrum.UpdateFft(e.Result));
    }

    private void OnMaxSample(object? sender, MaxSampleEventArgs e)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() => _vuMeter.UpdateLevel(Math.Abs(e.MaxValue)));
    }

    private void UpdateStreamStatusBar()
    {
        bool on = StreamManager.Instance.IsStreaming;
        bool onAir = on || MicrophoneManager.Instance.IsActive;
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
        PopulatePlaylistCombo();
        LoadCurrentPlaylist();
        LoadSession();
        // If session restored a different playlist, refresh the view
        PopulatePlaylistDisplayFields();
        RefreshPlaylistView();
        ShowNextTrack();
        UpdateStreamStatusBar();
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
        if (!string.IsNullOrWhiteSpace(item.Artist) && !string.IsNullOrWhiteSpace(item.Title))
            return;

        var track = MusicDb?.GetById(item.TrackId);
        if (track == null) return;
        item.Artist = track.Artist;
        item.Title = track.Title;
        if (!item.Duration.HasValue || item.Duration.Value <= TimeSpan.Zero)
            item.Duration = track.Duration > TimeSpan.Zero ? track.Duration : null;
    }

    private void PlayNext()
    {
        if (_currentPlaylist == null) return;
        _currentIndex++;
        if (_currentIndex >= _currentPlaylist.Items.Count)
        {
            _currentIndex = _currentPlaylist.Items.Count - 1;
            return;
        }
        PlayCurrent();
        ShowNextTrack();
    }

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

    private void BtnTalkover_Click(object? sender, EventArgs e)
    {
        if (MicrophoneManager.Instance.IsActive)
        {
            MicrophoneManager.Instance.StopTalkover();
            _btnTalkover.Text = "⏵ TALKOVER";
            _btnTalkover.BackColor = Color.FromArgb(60, 30, 30);
            _btnTalkover.ForeColor = Color.White;
            _micLevelBar.Value = 0;
            UpdateStreamStatusBar();
        }
        else
        {
            MicrophoneManager.Instance.DeviceNumber = _cboMicDevice.SelectedIndex >= 0 ? _cboMicDevice.SelectedIndex : 0;
            MicrophoneManager.Instance.DuckLevel = _duckingSlider.Value / 100f;
            try
            {
                MicrophoneManager.Instance.StartTalkover();
                _btnTalkover.Text = "⏹ STOP TALKOVER";
                _btnTalkover.BackColor = Color.FromArgb(180, 30, 30);
                _btnTalkover.ForeColor = Color.Yellow;
                UpdateStreamStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start microphone:\n{ex.Message}", "GoonNet",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void BtnBrowseTransition_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Transition / Stinger Sound",
            Filter = "Audio Files|*.mp3;*.wav;*.ogg;*.aac;*.flac|All Files|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtTransitionSound.Text = dlg.FileName;
    }

    private void BtnFadeAndNext_Click(object? sender, EventArgs e)
    {
        if (!AudioEngine.Instance.IsPlaying)
        {
            // Not playing — just advance
            PlayNext();
            ApplyPendingTransitionFadeIn();
            return;
        }

        var fadeDuration = TimeSpan.FromSeconds((double)_nudFadeSecs.Value);
        _pendingTransitionFadeIn = TimeSpan.FromSeconds((double)_nudFadeInSecs.Value);
        string stingerPath = _txtTransitionSound.Text.Trim();

        if (!string.IsNullOrEmpty(stingerPath) && File.Exists(stingerPath))
        {
            // Schedule the stinger to play after the fade
            _transitionTrack = new MusicTrack
            {
                Artist = "Transition",
                Title = Path.GetFileNameWithoutExtension(stingerPath),
                FileName = Path.GetFileName(stingerPath),
                Location = Path.GetDirectoryName(stingerPath) ?? string.Empty
            };
            MarkCurrentPlayed();
            AudioEngine.Instance.FadeOut(AudioDeviceType.Main, fadeDuration);
            // TrackEnded will fire → plays stinger → TrackEnded fires again → PlayNext()
        }
        else
        {
            // No stinger — just fade out and advance
            MarkCurrentPlayed();
            AudioEngine.Instance.FadeOut(AudioDeviceType.Main, fadeDuration, () =>
            {
                if (IsHandleCreated)
                    BeginInvoke(() =>
                    {
                        PlayNext();
                        ApplyPendingTransitionFadeIn();
                    });
            });
        }
    }

    private void ApplyPendingTransitionFadeIn()
    {
        var fadeIn = _pendingTransitionFadeIn;
        _pendingTransitionFadeIn = null;
        if (fadeIn == null || fadeIn.Value <= TimeSpan.Zero) return;
        if (AudioEngine.Instance.IsPlaying)
            AudioEngine.Instance.FadeIn(AudioDeviceType.Main, fadeIn.Value);
    }

    private void LvPlaylist_DoubleClick(object? sender, EventArgs e)
    {
        if (_lvPlaylist.SelectedItems.Count == 0) return;
        int idx = (int)(_lvPlaylist.SelectedItems[0].Tag ?? 0);
        MarkCurrentPlayed();
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
        if (MicrophoneManager.Instance.IsActive)
            MicrophoneManager.Instance.StopTalkover();

        // Safely unsubscribe
        AudioEngine.Instance.MainSampleAggregatorChanged -= OnMainAggregatorChanged;
        if (_currentAggregator != null)
        {
            _currentAggregator.FftDataAvailable -= OnFftData;
            _currentAggregator.MaximumCalculated -= OnMaxSample;
        }

        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Common radio-style hotkeys for quick on-air operation.
        if (keyData == Keys.Space) { BtnPlayPause_Click(this, EventArgs.Empty); return true; }
        if (keyData == (Keys.Control | Keys.Right)) { BtnNext_Click(this, EventArgs.Empty); return true; }
        if (keyData == (Keys.Control | Keys.F)) { BtnFadeAndNext_Click(this, EventArgs.Empty); return true; }
        if (keyData == Keys.F8) { BtnTalkover_Click(this, EventArgs.Empty); return true; }
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
            if (MicrophoneManager.Instance.IsActive) MicrophoneManager.Instance.StopTalkover();
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
            _micLevelBar.Value = 0;

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

