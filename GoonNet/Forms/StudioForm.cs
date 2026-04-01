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

    private System.Windows.Forms.Timer _clockTimer = null!;

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
        Size = new Size(920, 520);
        MinimumSize = new Size(820, 480);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        // ---- CLOCK ----
        _lblClock = new Label
        {
            Text = DateTime.Now.ToString("HH:mm:ss"),
            Font = new Font("Microsoft Sans Serif", 22f, FontStyle.Bold),
            ForeColor = Color.Navy,
            Size = new Size(200, 40),
            Location = new Point(700, 8),
            TextAlign = ContentAlignment.MiddleRight
        };

        // ---- NOW PLAYING PANEL ----
        var nowPanel = new GroupBox
        {
            Text = "NOW PLAYING",
            Location = new Point(8, 8),
            Size = new Size(360, 180),
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
            Size = new Size(280, 18),
            Minimum = 0,
            Maximum = 1000,
            Style = ProgressBarStyle.Continuous
        };
        _lblElapsed = new Label
        {
            Text = "0:00",
            Location = new Point(6, 90),
            Size = new Size(80, 16),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        _lblRemaining = new Label
        {
            Text = "0:00",
            Location = new Point(220, 90),
            Size = new Size(80, 16),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Microsoft Sans Serif", 8f)
        };

        _vuMeter = new VUMeter { Location = new Point(296, 64), Size = new Size(56, 108) };

        var lblIntro = new Label { Text = "Intro:", Location = new Point(6, 116), Size = new Size(40, 14), Font = new Font("Microsoft Sans Serif", 7f) };
        var lblVoiceOut = new Label { Text = "VoiceOut:", Location = new Point(80, 116), Size = new Size(55, 14), Font = new Font("Microsoft Sans Serif", 7f) };
        var lblMixIn = new Label { Text = "MixIn:", Location = new Point(170, 116), Size = new Size(40, 14), Font = new Font("Microsoft Sans Serif", 7f) };

        nowPanel.Controls.AddRange(new Control[] { _lblArtist, _lblTitle, _progressBar, _lblElapsed, _lblRemaining, _vuMeter, lblIntro, lblVoiceOut, lblMixIn });

        // ---- PLAYBACK CONTROLS ----
        var ctrlPanel = new GroupBox
        {
            Text = "CONTROLS",
            Location = new Point(378, 8),
            Size = new Size(220, 180),
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

        var lblVol = new Label { Text = "Volume:", Location = new Point(8, 130), Size = new Size(55, 16) };
        _volumeSlider = new TrackBar
        {
            Location = new Point(65, 124),
            Size = new Size(120, 30),
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
        _lblVolume = new Label { Text = "85%", Location = new Point(188, 130), Size = new Size(28, 16) };

        _chkAutoPlay = new CheckBox { Text = "Auto-Play", Location = new Point(8, 158), Checked = true };
        _chkAutoPlay.CheckedChanged += (s, e) => _autoPlay = _chkAutoPlay.Checked;

        ctrlPanel.Controls.AddRange(new Control[] { _btnPlayPause, _btnStop, _btnNext, _btnCue, _btnFadeOut, lblVol, _volumeSlider, _lblVolume, _chkAutoPlay });

        // ---- UP NEXT PANEL ----
        var nextPanel = new GroupBox
        {
            Text = "UP NEXT",
            Location = new Point(608, 8),
            Size = new Size(290, 100),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        _lblNextArtist = new Label { Text = "---", Location = new Point(6, 18), Size = new Size(278, 18), Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold) };
        _lblNextTitle = new Label { Text = string.Empty, Location = new Point(6, 38), Size = new Size(278, 16) };
        _lblNextDuration = new Label { Text = string.Empty, Location = new Point(6, 58), Size = new Size(150, 14), Font = new Font("Microsoft Sans Serif", 7f) };
        _btnLoadNext = new Button { Text = "Load", Location = new Point(158, 54), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnLoadNext.Click += BtnLoadNext_Click;
        nextPanel.Controls.AddRange(new Control[] { _lblNextArtist, _lblNextTitle, _lblNextDuration, _btnLoadNext });

        // ---- PREVIEW PANEL ----
        var previewPanel = new GroupBox
        {
            Text = "PREVIEW / MONITOR",
            Location = new Point(608, 116),
            Size = new Size(290, 72),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };
        _lblPreviewTrack = new Label { Text = "No preview loaded", Location = new Point(6, 18), Size = new Size(278, 14) };
        _btnPreviewPlay = new Button { Text = "▶", Location = new Point(6, 36), Size = new Size(36, 24), FlatStyle = FlatStyle.System };
        _btnPreviewPlay.Click += (s, e) => { /* Preview play requires a track to be loaded via queue */ };
        _btnPreviewStop = new Button { Text = "■", Location = new Point(46, 36), Size = new Size(36, 24), FlatStyle = FlatStyle.System };
        _btnPreviewStop.Click += (s, e) => AudioEngine.Instance.Stop(AudioDeviceType.Preview);
        var lblPreviewVol = new Label { Text = "Vol:", Location = new Point(90, 40), Size = new Size(28, 16) };
        _previewVolumeSlider = new TrackBar { Location = new Point(118, 34), Size = new Size(110, 28), Minimum = 0, Maximum = 100, Value = 80, TickFrequency = 20 };
        _previewVolumeSlider.ValueChanged += (s, e) => AudioEngine.Instance.PreviewVolume = _previewVolumeSlider.Value / 100f;
        previewPanel.Controls.AddRange(new Control[] { _lblPreviewTrack, _btnPreviewPlay, _btnPreviewStop, lblPreviewVol, _previewVolumeSlider });

        // ---- PLAYLIST LIST ----
        var playlistPanel = new GroupBox
        {
            Text = "CURRENT PLAYLIST",
            Location = new Point(8, 198),
            Size = new Size(888, 270),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold)
        };

        _lvPlaylist = new ListView
        {
            Location = new Point(6, 18),
            Size = new Size(876, 242),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvPlaylist.Columns.Add("#", 30);
        _lvPlaylist.Columns.Add("Artist", 160);
        _lvPlaylist.Columns.Add("Title", 200);
        _lvPlaylist.Columns.Add("Duration", 70);
        _lvPlaylist.Columns.Add("Start", 80);
        _lvPlaylist.Columns.Add("Status", 80);
        _lvPlaylist.DoubleClick += LvPlaylist_DoubleClick;

        playlistPanel.Controls.Add(_lvPlaylist);

        Controls.AddRange(new Control[] { _lblClock, nowPanel, ctrlPanel, nextPanel, previewPanel, playlistPanel });
    }

    private void ConnectAudioEngine()
    {
        AudioEngine.Instance.TrackStarted += (s, e) =>
        {
            if (e.Device != AudioDeviceType.Main) return;
            BeginInvoke(() =>
            {
                var t = e.Track;
                if (t != null)
                {
                    _lblArtist.Text = t.Artist;
                    _lblTitle.Text = t.Title;
                    _btnPlayPause.Text = "⏸ PAUSE";
                    _btnPlayPause.BackColor = Color.FromArgb(255, 255, 180);
                }
            });
        };

        AudioEngine.Instance.TrackEnded += (s, e) =>
        {
            if (e.Device != AudioDeviceType.Main) return;
            BeginInvoke(() =>
            {
                _btnPlayPause.Text = "▶ PLAY";
                _btnPlayPause.BackColor = Color.FromArgb(200, 255, 200);
                _progressBar.Value = 0;
                _lblElapsed.Text = "0:00";
                _lblRemaining.Text = "0:00";
                MarkCurrentPlayed();
                if (_autoPlay) PlayNext();
            });
        };

        AudioEngine.Instance.TrackPositionChanged += (s, e) =>
        {
            BeginInvoke(() =>
            {
                _progressBar.Value = (int)(e.Fraction * 1000);
                _lblElapsed.Text = FormatTime(e.Position);
                _lblRemaining.Text = "-" + FormatTime(e.Duration - e.Position);
                // VU meter uses a simulated level - real implementation would read
                // audio sample peak values from the NAudio SampleProvider pipeline
                _vuMeter.UpdateLevel(Math.Clamp(0.4 + Math.Sin(e.Position.TotalSeconds * 3) * 0.35, 0, 1));
            });
        };
    }

    private void StudioForm_Load(object? sender, EventArgs e)
    {
        LoadCurrentPlaylist();
    }

    private void LoadCurrentPlaylist()
    {
        _currentPlaylist = PlaylistDb?.GetCurrentPlaylist();
        if (_currentPlaylist == null)
        {
            _currentPlaylist = new Playlist { Name = "Empty Playlist" };
        }
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
        var nextIdx = _currentIndex + 1;
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
        {
            track = new MusicTrack { Artist = item.Artist, Title = item.Title, FileName = string.Empty };
        }

        if (!File.Exists(track.FullPath))
        {
            MessageBox.Show($"File not found: {track.FullPath}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private void LvPlaylist_DoubleClick(object? sender, EventArgs e)
    {
        if (_lvPlaylist.SelectedItems.Count == 0) return;
        var idx = (int)(_lvPlaylist.SelectedItems[0].Tag ?? 0);
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
        base.OnFormClosed(e);
    }
}
