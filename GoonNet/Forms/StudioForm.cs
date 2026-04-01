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
    private Label _lblClock = null!;
    private ProgressBar _progressBar = null!;
    private VUMeter _vuMeter = null!;

    // Controls
    private Button _btnPlayPause = null!;
    private Button _btnStop = null!;
    private Button _btnNext = null!;
    private Button _btnCue = null!;
    private Button _btnFadeOut = null!;
    private TrackBar _volumeSlider = null!;
    private Label _lblVolume = null!;
    private CheckBox _chkAutoPlay = null!;

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
    private Button _btnFadeAndNext = null!;

    // Transition playback state
    private bool _playingTransition;
    private MusicTrack? _transitionTrack;

    private System.Windows.Forms.Timer _clockTimer = null!;

    // SampleAggregator subscription tracking
    private SampleAggregator? _currentAggregator;

    public StudioForm()
    {
        InitializeComponent();
        ConnectAudioEngine();
        _clockTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _clockTimer.Tick += (s, e) => _lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
        Load += StudioForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Studio";
        Size = new Size(1060, 740);
        MinimumSize = new Size(860, 640);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

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
        var lblVoiceOut = new Label { Text = "VoiceOut:", Location = new Point(78, 114), Size = new Size(55, 13), Font = new Font("Microsoft Sans Serif", 7f) };
        var lblMixIn = new Label { Text = "MixIn:", Location = new Point(165, 114), Size = new Size(40, 13), Font = new Font("Microsoft Sans Serif", 7f) };

        nowPanel.Controls.AddRange(new Control[] { _lblArtist, _lblTitle, _progressBar, _lblElapsed, _lblRemaining, _vuMeter, lblIntro, lblVoiceOut, lblMixIn });

        // ---- PLAYBACK CONTROLS ----
        var ctrlPanel = new GroupBox
        {
            Text = "CONTROLS",
            Location = new Point(376, 4),
            Size = new Size(222, 186),
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

        var lblVol = new Label { Text = "Volume:", Location = new Point(8, 130), Size = new Size(52, 16) };
        _volumeSlider = new TrackBar
        {
            Location = new Point(62, 124),
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
        _lblVolume = new Label { Text = "85%", Location = new Point(183, 130), Size = new Size(30, 16) };

        _chkAutoPlay = new CheckBox { Text = "Auto-Play", Location = new Point(8, 160), Checked = true };
        _chkAutoPlay.CheckedChanged += (s, e) => _autoPlay = _chkAutoPlay.Checked;

        ctrlPanel.Controls.AddRange(new Control[] { _btnPlayPause, _btnStop, _btnNext, _btnCue, _btnFadeOut, lblVol, _volumeSlider, _lblVolume, _chkAutoPlay });

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

        rightPanel.Controls.AddRange(new Control[] { _lblClock, nextPanel, previewPanel });
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
        // STUDIO TOOLS BAR  (transition sounds + microphone talkover)
        // ══════════════════════════════════════════════════════════════════════
        _studioToolsBar = BuildStudioToolsBar();
        _studioToolsBar.Location = new Point(0, 224);
        _studioToolsBar.SizeChanged += (s, e) => _studioToolsBar.Width = ClientSize.Width;

        // ══════════════════════════════════════════════════════════════════════
        // LARGE SPECTRUM ANALYZER
        // ══════════════════════════════════════════════════════════════════════
        var spectrumPanel = new GroupBox
        {
            Text = "SPECTRUM ANALYZER",
            Location = new Point(0, 304),   // 224 + 80 toolsBar height
            Height = 180,
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
            Location = new Point(0, 484),   // 304 + 180 = 484
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
            _studioToolsBar.Width = ClientSize.Width;
            spectrumPanel.Width = ClientSize.Width;
            rightPanel.Width = ClientSize.Width - 614;

            playlistPanel.Location = new Point(0, 484);
            playlistPanel.Size = new Size(ClientSize.Width, ClientSize.Height - 484);
            _lvPlaylist.Size = new Size(playlistPanel.ClientSize.Width - 8, playlistPanel.ClientSize.Height - 22);

            _btnStreamSettings.Location = new Point(streamBar.Width - 130, 3);
        };

        Controls.AddRange(new Control[] { topPanel, streamBar, _studioToolsBar, spectrumPanel, playlistPanel });

        // Fire SizeChanged once to initialize sizes
        OnSizeChanged(EventArgs.Empty);
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

        var lblTransTip = new Label
        {
            Text = "Fades out current track and (optionally) plays a stinger before auto-advancing.",
            Location = new Point(8, 48),
            Size = new Size(530, 18),
            ForeColor = Color.FromArgb(120, 130, 150),
            Font = new Font("Microsoft Sans Serif", 7f)
        };

        transGroup.Controls.AddRange(new Control[] { lblFade, _nudFadeSecs, lblSecs, lblSnd, _txtTransitionSound, btnBrowse, _btnFadeAndNext, lblTransTip });

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
                MarkCurrentPlayed();

                if (_playingTransition)
                {
                    // The transition stinger just finished — advance playlist normally
                    _playingTransition = false;
                    _transitionTrack = null;
                    if (_autoPlay) PlayNext();
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
                        if (_autoPlay) PlayNext();
                    }
                }
                else
                {
                    if (_autoPlay) PlayNext();
                }
            });
        };

        AudioEngine.Instance.TrackPositionChanged += (s, e) =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                _progressBar.Value = (int)(e.Fraction * 1000);
                _lblElapsed.Text = FormatTime(e.Position);
                _lblRemaining.Text = "-" + FormatTime(e.Duration - e.Position);
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
        if (on)
        {
            int listeners = StreamManager.Instance.ListenerCount;
            string url = $"http://localhost:{StreamManager.Instance.Port}/stream";
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
        LoadCurrentPlaylist();
    }

    private void LoadCurrentPlaylist()
    {
        _currentPlaylist = PlaylistDb?.GetCurrentPlaylist();
        if (_currentPlaylist == null)
            _currentPlaylist = new Playlist { Name = "Empty Playlist" };
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
        var track = MusicDb?.GetById(item.TrackId);
        if (track == null)
            track = new MusicTrack { Artist = item.Artist, Title = item.Title, FileName = string.Empty };

        if (!File.Exists(track.FullPath))
        {
            MessageBox.Show($"File not found:\n{track.FullPath}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        item.ActualStart = DateTime.Now;
        AudioEngine.Instance.PlayTrack(track);
        MusicDb?.UpdatePlayStats(track.Id);
        LogDb?.AddEntry(track.Artist, track.Title, track.FileName, EventType.Music);
        RefreshPlaylistView();
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
            return;
        }

        var fadeDuration = TimeSpan.FromSeconds((double)_nudFadeSecs.Value);
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
                    BeginInvoke(() => PlayNext());
            });
        }
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
}
